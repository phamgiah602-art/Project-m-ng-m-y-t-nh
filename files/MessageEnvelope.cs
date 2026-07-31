using System.Text.Json;

namespace RemoteControlLAN.Shared.Messages;

/// <summary>
/// Khung message chung cho MỌI action trao đổi qua WebSocket
/// (xem docs/kien-truc-chi-tiet.md mục 1 — "Quy ước chung").
///
/// Vì mỗi action có 1 kiểu payload khác nhau (xem Payloads.cs), lúc mới đọc message ta
/// CHƯA biết payload là DTO gì — nên Payload được giữ tạm dạng JsonElement (JSON thô).
/// Khi đã biết "Action" là gì (ví dụ "SHUTDOWN"), gọi envelope.GetPayload&lt;ShutdownPayload&gt;()
/// để chuyển sang đúng DTO cụ thể.
/// </summary>
public class MessageEnvelope
{
    /// <summary>COMMAND | RESPONSE | STREAM_FRAME | FILE_CHUNK | EVENT | PING | PONG</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Tên action, ví dụ "GET_PROCESS_LIST", "SHUTDOWN", "SCREEN_FRAME"...</summary>
    public string Action { get; set; } = string.Empty;

    public string? SessionId { get; set; }
    public string? AgentId { get; set; }
    public string? ConnectionId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>Payload thô — dùng GetPayload&lt;T&gt;() để lấy ra DTO cụ thể.</summary>
    public JsonElement Payload { get; set; }

    /// <summary>Chuyển Payload thô sang đúng DTO cụ thể (xem Payloads.cs cho danh sách DTO).</summary>
    public T? GetPayload<T>() => Payload.ValueKind == JsonValueKind.Undefined
        ? default
        : Payload.Deserialize<T>(JsonConfig.Default);

    /// <summary>
    /// Helper tạo 1 message mới để gửi đi — dùng ở mọi nơi cần trả lời hoặc phát (emit)
    /// message mới, thay vì tự tay dựng JsonElement bằng tay dễ sai.
    /// </summary>
    public static MessageEnvelope Create(
        string type,
        string action,
        object payload,
        string? sessionId = null,
        string? agentId = null,
        string? connectionId = null)
    {
        return new MessageEnvelope
        {
            Type = type,
            Action = action,
            SessionId = sessionId,
            AgentId = agentId,
            ConnectionId = connectionId,
            Timestamp = DateTime.UtcNow,
            Payload = JsonSerializer.SerializeToElement(payload, JsonConfig.Default)
        };
    }
}
