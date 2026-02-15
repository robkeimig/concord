using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Concord.Endpoints;

public static class ConcordEndpoints
{
    public static IEndpointRouteBuilder MapConcordEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Simple single-room signaling/orchestration state
        var clients = new ConcurrentDictionary<string, WebSocket>();
        var activeStreams = new ConcurrentDictionary<string, bool>(); // clientId -> isStreaming

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

        endpoints.Map("/ws", async context =>
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

        return endpoints;
    }
}
