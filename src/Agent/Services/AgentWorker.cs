using System.Security.Cryptography;
using RemoteControlLAN.Agent.Configuration;
using RemoteControlLAN.Agent.Platform;
using RemoteControlLAN.Shared.Messages;

namespace RemoteControlLAN.Agent.Services;
public sealed class AgentWorker(GatewayConnection gateway, AgentProcessor processor, AgentOptions options, INotificationService notifications, ILogger<AgentWorker> logger) : BackgroundService
{
    private CancellationTokenSource? _pinRefreshCts;
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        gateway.MessageReceived += message => processor.HandleAsync(message, stoppingToken);
        processor.SessionStarted += () => { _pinRefreshCts?.Cancel(); logger.LogInformation("PIN auto-refresh paused (session active)"); };
        processor.SessionEnded += () => StartPinRefreshLoop(stoppingToken);
        processor.AgentRegistered += () => StartPinRefreshLoop(stoppingToken);
        logger.LogInformation("Agent started");
        await gateway.RunAsync(stoppingToken);
    }
    private void StartPinRefreshLoop(CancellationToken stoppingToken)
    {
        _pinRefreshCts?.Cancel();
        _pinRefreshCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var token = _pinRefreshCts.Token;
        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(3), token);
                    if (token.IsCancellationRequested) break;
                    var pin = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
                    logger.LogInformation("\n============================================\n  MÃ PIN MỚI (AUTO-REFRESH): {Pin} (Hết hạn sau 5 phút)\n============================================", pin);
                    await notifications.ShowAsync("Remote Control LAN", $"Mã ghép cặp mới: {pin}. Hết hạn sau 5 phút.", token);
                    await gateway.SendAsync(MessageEnvelope.Create("EVENT", "UPDATE_PAIRING_PIN", new PinPayload { Pin = pin }, agentId: options.AgentId), token);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Lỗi khi tự động sinh mã PIN mới; sẽ thử lại.");
                    await Task.Delay(TimeSpan.FromSeconds(15), token);
                }
            }
        }, token);
    }
}
