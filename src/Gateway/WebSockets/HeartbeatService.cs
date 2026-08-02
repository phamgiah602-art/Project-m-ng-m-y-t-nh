using RemoteControlLAN.Shared.Messages;

namespace RemoteControlLAN.Gateway.WebSockets;

public sealed class HeartbeatService(ConnectionManager connections, MessageRouter router, ILogger<HeartbeatService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            foreach (var entry in connections.All.ToArray())
            {
                if (DateTime.UtcNow - entry.Value.LastSeenAt > TimeSpan.FromSeconds(40))
                {
                    logger.LogInformation("Heartbeat timeout: {ConnectionId}", entry.Key);
                    foreach (var session in connections.Remove(entry.Key)) await router.NotifyDisconnectAsync(session, entry.Key);
                    try { entry.Value.Socket.Abort(); } catch { }
                    continue;
                }
                await router.SendAsync(entry.Key, MessageEnvelope.Create("PING", "PING", new EmptyPayload()));
            }
        }
    }
}
