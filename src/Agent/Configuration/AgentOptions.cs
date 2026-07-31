namespace RemoteControlLAN.Agent.Configuration;

public sealed class AgentOptions
{
    public string GatewayUrl { get; set; } = "ws://localhost:5000/ws";
    public string AgentId { get; set; } = string.Empty;
    public string AgentSecretKey { get; set; } = string.Empty;
    public bool AllowPowerCommands { get; set; } = false;
    public List<string> AdditionalBlockedPaths { get; set; } = [];
    public List<string> AdditionalProtectedProcesses { get; set; } = [];
}
