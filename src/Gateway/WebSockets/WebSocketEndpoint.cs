using System.Net.WebSockets;
using System.Text;

namespace RemoteControlLAN.Gateway.WebSockets;

public sealed class WebSocketEndpoint(ConnectionManager connections, MessageRouter router, ILogger<WebSocketEndpoint> logger)
{
    public async Task HandleAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest) { context.Response.StatusCode = StatusCodes.Status400BadRequest; return; }
        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        var id = Guid.NewGuid().ToString(); var userId = context.User.Identity?.IsAuthenticated == true ? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? context.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value : null;
        connections.Add(id, socket, userId);
        try
        {
            var buffer = new byte[64 * 1024];
            while (socket.State == WebSocketState.Open)
            {
                using var stream = new MemoryStream(); WebSocketReceiveResult result;
                do { result = await socket.ReceiveAsync(buffer, CancellationToken.None); if (result.MessageType == WebSocketMessageType.Close) break; stream.Write(buffer, 0, result.Count); if (stream.Length > 5 * 1024 * 1024) throw new InvalidOperationException("Message quá lớn."); } while (!result.EndOfMessage);
                if (result.MessageType == WebSocketMessageType.Close) break;
                await router.RouteAsync(Encoding.UTF8.GetString(stream.ToArray()), id);
            }
        }
        catch (Exception ex) { logger.LogWarning(ex, "WebSocket {ConnectionId} bị ngắt", id); }
        finally { foreach (var session in connections.Remove(id)) await router.NotifyDisconnectAsync(session); if (socket.State == WebSocketState.Open) await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed", CancellationToken.None); }
    }
}
