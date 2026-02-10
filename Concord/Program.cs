using System.Net.WebSockets;
using System.Text;
using Concord.Services;
using Concord.Services.Acme;

List<WebSocket> _sockets = new();

var builder = WebApplication.CreateBuilder(args);

// Register services
builder.Services.AddHttpClient<IPublicIpService, AmazonPublicIpService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});

// ACME & certificate services
var dataDir = Path.Combine(AppContext.BaseDirectory, "data");
var acmeDir = Path.Combine(dataDir, "acme");
var certDir = Path.Combine(dataDir, "certs");
Directory.CreateDirectory(acmeDir);
Directory.CreateDirectory(certDir);

var tlsProvider = new CurrentCertificateProvider();

builder.Services.AddSingleton<IAcmeHttpChallengeStore, AcmeHttpChallengeStore>();
builder.Services.AddSingleton<IAcmeAccountStore>(_ => new FileAcmeAccountStore(acmeDir));
builder.Services.AddSingleton<ICertificateStore>(_ => new FileCertificateStore(certDir));
builder.Services.AddHttpClient<IAcmeClient, AcmeClient>().ConfigureHttpClient(client => client.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddSingleton<ICurrentCertificateProvider>(tlsProvider);
builder.Services.AddSingleton<ICertificateManager, CertificateManager>();
builder.Services.AddHostedService<CertificateRenewalService>();

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(80);
    options.ListenAnyIP(443, listenOptions =>
    {
        listenOptions.UseHttps((httpsOptions) => 
        {
            httpsOptions.ServerCertificateSelector = (ctx, name) => tlsProvider.Current;
        });
    });
});

var app = builder.Build();
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
    {
        return Results.Text(keyAuth, "text/plain", Encoding.UTF8);
    }
    return Results.NotFound();
});

// Public IP endpoint
app.MapGet("/ip", async (IPublicIpService ipService, CancellationToken ct) =>
{
    var ip = await ipService.GetPublicIpAsync(ct);
    return Results.Text(ip + "\n", "text/plain", Encoding.UTF8);
});

app.Map("/ws", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    await HandleWebSocketAsync(socket);
});

async Task HandleWebSocketAsync(WebSocket socket)
{
    lock (_sockets)
        _sockets.Add(socket);

    var buffer = new byte[8192];

    try
    {
        while (socket.State == WebSocketState.Open)
        {
            var result = await socket.ReceiveAsync(
                new ArraySegment<byte>(buffer),
                CancellationToken.None
            );

            if (result.MessageType == WebSocketMessageType.Close)
                break;

            foreach (var peer in _sockets)
            {
                if (peer != socket && peer.State == WebSocketState.Open)
                {
                    await peer.SendAsync(
                        new ArraySegment<byte>(buffer, 0, result.Count),
                        WebSocketMessageType.Text,
                        true,
                        CancellationToken.None
                    );
                }
            }
        }
    }
    finally
    {
        lock (_sockets)
            _sockets.Remove(socket);

        await socket.CloseAsync(
            WebSocketCloseStatus.NormalClosure,
            "bye",
            CancellationToken.None
        );
    }
}

app.Run();