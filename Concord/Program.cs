using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Concord.Services;
using Concord.Services.Acme;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient<IPublicIpService, AmazonPublicIpService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});

// ACME services
var dataDir = Path.Combine(AppContext.BaseDirectory, "data");
var acmeDir = Path.Combine(dataDir, "acme");
Directory.CreateDirectory(acmeDir);

builder.Services.AddSingleton<IAcmeHttpChallengeStore, AcmeHttpChallengeStore>();
builder.Services.AddSingleton<IAcmeAccountStore>(_ => new FileAcmeAccountStore(acmeDir));
builder.Services.AddHttpClient<IAcmeClient, AcmeClient>().ConfigureHttpClient(client => client.Timeout = TimeSpan.FromSeconds(30));

// Cert state kept in-memory and refreshed synchronously during certificate selection.
object certLock = new();
X509Certificate2? currentCert = null;
DateTimeOffset? issuedAtUtc = null;
string? issuedForIp = null;

IServiceProvider? serviceProvider = null;

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(80);
    options.ListenAnyIP(443, listenOptions =>
    {
        listenOptions.UseHttps(httpsOptions =>
        {
            httpsOptions.ServerCertificateSelector = (connectionContext, name) =>
            {
                lock (certLock)
                {
                    var now = DateTimeOffset.UtcNow;
                    var needsRenew = currentCert is null
                        || issuedAtUtc is null
                        || (now - issuedAtUtc.Value) >= TimeSpan.FromHours(24);

                    if (!needsRenew)
                        return currentCert!;

                    var sp = serviceProvider ?? throw new InvalidOperationException("ServiceProvider not initialized");
                    using var scope = sp.CreateScope();
                    var ipService = scope.ServiceProvider.GetRequiredService<IPublicIpService>();
                    var acme = scope.ServiceProvider.GetRequiredService<IAcmeClient>();
                    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("KestrelCertSelector");

                    var ip = ipService.GetPublicIpAsync(CancellationToken.None).GetAwaiter().GetResult();

                    if (issuedForIp is not null && !string.Equals(issuedForIp, ip, StringComparison.Ordinal))
                        logger.LogInformation("Public IP changed from {OldIp} to {NewIp}; re-issuing certificate", issuedForIp, ip);

                    logger.LogInformation("Issuing/renewing Let's Encrypt certificate for {Ip}", ip);
                    currentCert = acme.EnsureIpCertificateAsync(ip, CancellationToken.None).GetAwaiter().GetResult();

                    issuedAtUtc = now;
                    issuedForIp = ip;

                    return currentCert;
                }
            };
        });
    });
});

var app = builder.Build();
serviceProvider = app.Services;

app.Logger.LogInformation("Concord starting");

app.UseDefaultFiles();
app.UseStaticFiles();

var wsOptions = new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30)
};

app.UseWebSockets(wsOptions);

// ACME HTTP-01 challenge endpoint
app.MapGet("/.well-known/acme-challenge/{token}", (string token, IAcmeHttpChallengeStore store) =>
{
    if (store.TryGet(token, out var keyAuth))
        return Results.Text(keyAuth, "text/plain", Encoding.UTF8);

    return Results.NotFound();
});

// Public IP endpoint
app.MapGet("/ip", async (IPublicIpService ipService, CancellationToken ct) =>
{
    var ip = await ipService.GetPublicIpAsync(ct);
    return Results.Text(ip + "\n", "text/plain", Encoding.UTF8);
});

// Simple single-room signaling/orchestration state
var clients = new ConcurrentDictionary<string, WebSocket>();
var activeStreams = new ConcurrentDictionary<string, bool>(); // clientId -> isStreaming

app.Map("/ws", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    var clientId = Guid.NewGuid().ToString("n");

    clients[clientId] = socket;

    // initial hello (send id + current streamers)
    await SendAsync(socket, new
    {
        type = "hello",
        clientId,
        streamers = activeStreams.Keys.OrderBy(x => x).ToArray()
    }, context.RequestAborted);

    // notify others someone joined (optional, but helps UI)
    await BroadcastAsync(new { type = "peer-joined", clientId }, exceptClientId: clientId, context.RequestAborted);

    try
    {
        await ReceiveLoopAsync(clientId, socket, context.RequestAborted);
    }
    finally
    {
        clients.TryRemove(clientId, out _);
        activeStreams.TryRemove(clientId, out _);

        await BroadcastAsync(new { type = "peer-left", clientId }, exceptClientId: null, CancellationToken.None);
        await BroadcastStreamersAsync(CancellationToken.None);

        try
        {
            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
        }
        catch
        {
            // ignore
        }
    }
});

app.Run();

async Task ReceiveLoopAsync(string clientId, WebSocket socket, CancellationToken ct)
{
    var buffer = new byte[64 * 1024];

    while (!ct.IsCancellationRequested && socket.State == WebSocketState.Open)
    {
        var ms = new MemoryStream();
        WebSocketReceiveResult? result;
        do
        {
            result = await socket.ReceiveAsync(buffer, ct);
            if (result.MessageType == WebSocketMessageType.Close)
                return;

            ms.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);

        if (result.MessageType != WebSocketMessageType.Text)
            continue;

        var json = Encoding.UTF8.GetString(ms.ToArray());
        if (string.IsNullOrWhiteSpace(json))
            continue;

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("type", out var typeEl))
            continue;

        var type = typeEl.GetString();
        switch (type)
        {
            case "stream-start":
                activeStreams[clientId] = true;
                await BroadcastStreamersAsync(ct);
                break;

            case "stream-stop":
                activeStreams.TryRemove(clientId, out _);
                await BroadcastStreamersAsync(ct);
                break;

            // WebRTC signaling is always point-to-point. Client includes `to`.
            case "offer":
            case "answer":
            case "ice":
            case "hangup":
                if (!doc.RootElement.TryGetProperty("to", out var toEl))
                    break;
                var to = toEl.GetString();
                if (string.IsNullOrWhiteSpace(to))
                    break;

                // IMPORTANT: JsonElement values are backed by the JsonDocument. Clone them before the document is disposed.
                JsonElement? offer = doc.RootElement.TryGetProperty("offer", out var offerEl) ? offerEl.Clone() : null;
                JsonElement? answer = doc.RootElement.TryGetProperty("answer", out var answerEl) ? answerEl.Clone() : null;
                JsonElement? candidate = doc.RootElement.TryGetProperty("candidate", out var candEl) ? candEl.Clone() : null;

                await SendToAsync(to!, new
                {
                    type,
                    from = clientId,
                    offer,
                    answer,
                    candidate
                }, ct);
                break;

            default:
                break;
        }
    }
}

async Task BroadcastStreamersAsync(CancellationToken ct)
{
    var streamers = activeStreams.Keys.OrderBy(x => x).ToArray();
    await BroadcastAsync(new { type = "streamers", streamers }, exceptClientId: null, ct);
}

async Task BroadcastAsync(object payload, string? exceptClientId, CancellationToken ct)
{
    var json = JsonSerializer.Serialize(payload);
    var bytes = Encoding.UTF8.GetBytes(json);

    foreach (var kvp in clients)
    {
        if (exceptClientId != null && kvp.Key == exceptClientId)
            continue;

        var ws = kvp.Value;
        if (ws.State != WebSocketState.Open)
            continue;

        try
        {
            await ws.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
        }
        catch
        {
            // ignore
        }
    }
}

async Task SendToAsync(string clientId, object payload, CancellationToken ct)
{
    if (!clients.TryGetValue(clientId, out var ws))
        return;
    if (ws.State != WebSocketState.Open)
        return;

    await SendAsync(ws, payload, ct);
}

static async Task SendAsync(WebSocket ws, object payload, CancellationToken ct)
{
    // Support forwarding JsonElement values inside anonymous types
    var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    });
    var bytes = Encoding.UTF8.GetBytes(json);
    await ws.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
}