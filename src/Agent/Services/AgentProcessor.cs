using System.Security.Cryptography;
using RemoteControlLAN.Agent.Commands;
using RemoteControlLAN.Agent.Configuration;
using RemoteControlLAN.Agent.Platform;
using RemoteControlLAN.Shared.Messages;

namespace RemoteControlLAN.Agent.Services;

public sealed class AgentProcessor(AgentOptions options, GatewayConnection gateway, AgentCommandDispatcher commands, INotificationService notifications, ILogger<AgentProcessor> logger)
{
    public async Task HandleAsync(MessageEnvelope message, CancellationToken cancellationToken)
    {
        if (message.Action == "PING") { await gateway.SendAsync(MessageEnvelope.Create("PONG", "PONG", new EmptyPayload(), message.SessionId, options.AgentId), cancellationToken); return; }
        if (message.Action == "REGISTER_AGENT_RESULT")
        {
            var registered = message.GetPayload<RegisterAgentResultPayload>();
            if (registered?.Success != true) { logger.LogError("Gateway từ chối Agent: {Message}", registered?.Message); return; }
            var pin = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
            await notifications.ShowAsync("Remote Control LAN", $"Mã ghép cặp của máy này: {pin}. Mã hết hạn sau 5 phút.", cancellationToken);
            await gateway.SendAsync(MessageEnvelope.Create("EVENT", "UPDATE_PAIRING_PIN", new PinPayload { Pin = pin }, agentId: options.AgentId), cancellationToken); return;
        }
        if (message.Action == "PAIRING_RESULT" && message.GetPayload<PairingResultPayload>() is { Success: true }) { await notifications.ShowAsync("Remote Control LAN", "Máy đang được điều khiển trong một phiên đã được chấp thuận.", cancellationToken); return; }
        if (message.Action == "UPDATE_PAIRING_PIN_RESULT") return;
        await commands.ExecuteAsync(message, item => gateway.SendAsync(item, cancellationToken), cancellationToken);
    }
}
