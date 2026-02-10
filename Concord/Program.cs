using System.Net.WebSockets;
using System.Text;

List<WebSocket> _sockets = new();

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.UseDefaultFiles();  
app.UseStaticFiles();   

var wsOptions = new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30)
};

app.UseWebSockets(wsOptions);

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