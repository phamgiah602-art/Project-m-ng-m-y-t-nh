using System.Text.Json;
using System.Text.Json.Serialization;

namespace RemoteControlLAN.Shared.Messages;

/// <summary>
/// Cấu hình JSON DÙNG CHUNG cho toàn bộ hệ thống (cả Gateway và Agent phải dùng đúng 1 cấu
/// hình này). Mục đích: field JSON luôn là camelCase (agentId, sessionId...), trong khi
/// property C# vẫn viết PascalCase bình thường (AgentId, SessionId...) — .NET tự động
/// chuyển đổi qua lại theo PropertyNamingPolicy, không cần gắn [JsonPropertyName] thủ công
/// lên từng property.
///
/// Dùng ở MỌI nơi gọi JsonSerializer.Serialize / Deserialize liên quan tới message
/// WebSocket, ví dụ: JsonSerializer.Serialize(envelope, JsonConfig.Default).
/// </summary>
public static class JsonConfig
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // Không phân biệt hoa/thường khi ĐỌC JSON vào — phòng trường hợp phía Web Client
        // lỡ gõ nhầm case, tránh lỗi deserialize khó hiểu.
        PropertyNameCaseInsensitive = true
    };
}
