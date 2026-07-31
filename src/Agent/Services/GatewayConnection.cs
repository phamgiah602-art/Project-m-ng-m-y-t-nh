using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using RemoteControlLAN.Agent.Configuration;
using RemoteControlLAN.Shared.Messages;

namespace RemoteControlLAN.Agent.Services;

public sealed class GatewayConnection(AgentOptions options, ILogger<GatewayConnection> logger)
{
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private ClientWebSocket? _socket;
    public event Func<MessageEnvelope, Task>? MessageReceived;
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var retry = 1;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var socket = new ClientWebSocket(); _socket = socket; await socket.ConnectAsync(new Uri(options.GatewayUrl), cancellationToken); retry = 1;
                await SendAsync(MessageEnvelope.Create("EVENT", "REGISTER_AGENT", new RegisterAgentPayload { AgentSecretKey = options.AgentSecretKey, Platform = OperatingSystem.IsMacOS() ? "MacOS" : "Windows", Hostname = Environment.MachineName, AgentVersion = "1.0.0" }, agentId: options.AgentId), cancellationToken);
                await ReceiveLoopAsync(socket, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning(ex, "Mất kết nối Gateway; thử lại sau {Seconds}s", retry); }
            finally { _socket = null; }
            await Task.Delay(TimeSpan.FromSeconds(retry), cancellationToken); retry = Math.Min(retry * 2, 30);
        }
    }
    public async Task SendAsync(MessageEnvelope message, CancellationToken cancellationToken = default)
    {
        var socket = _socket; if (socket?.State != WebSocketState.Open) throw new InvalidOperationException("Gateway chưa kết nối.");
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message, JsonConfig.Default)); await _sendLock.WaitAsync(cancellationToken);
        try { await socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken); } finally { _sendLock.Release(); }
    }
    private async Task ReceiveLoopAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[64 * 1024]; while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            using var stream = new MemoryStream(); WebSocketReceiveResult result; do { result = await socket.ReceiveAsync(buffer, cancellationToken); if (result.MessageType == WebSocketMessageType.Close) return; stream.Write(buffer, 0, result.Count); } while (!result.EndOfMessage);
            if (result.MessageType != WebSocketMessageType.Text) continue; var envelope = JsonSerializer.Deserialize<MessageEnvelope>(Encoding.UTF8.GetString(stream.ToArray()), JsonConfig.Default); if (envelope is not null && MessageReceived is not null) await MessageReceived.Invoke(envelope);
        }
    }
}
