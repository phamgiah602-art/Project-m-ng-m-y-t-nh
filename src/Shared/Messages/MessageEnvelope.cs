using System.Text.Json;

namespace RemoteControlLAN.Shared.Messages;

public sealed class MessageEnvelope
{
    public string Type { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? SessionId { get; set; }
    public string? AgentId { get; set; }
    public string? ConnectionId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public JsonElement Payload { get; set; }

    public T? GetPayload<T>() => Payload.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
        ? default : Payload.Deserialize<T>(JsonConfig.Default);

    public static MessageEnvelope Create(string type, string action, object payload,
        string? sessionId = null, string? agentId = null, string? connectionId = null) => new()
    {
        Type = type, Action = action, SessionId = sessionId, AgentId = agentId,
        ConnectionId = connectionId, Timestamp = DateTime.UtcNow,
        Payload = JsonSerializer.SerializeToElement(payload, JsonConfig.Default)
    };
}
