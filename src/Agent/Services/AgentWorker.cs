namespace RemoteControlLAN.Agent.Services;
public sealed class AgentWorker(GatewayConnection gateway, AgentProcessor processor, ILogger<AgentWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) { gateway.MessageReceived += message => processor.HandleAsync(message, stoppingToken); logger.LogInformation("Agent started"); await gateway.RunAsync(stoppingToken); }
}
