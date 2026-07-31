namespace RemoteControlLAN.Shared.Messages;

// Mỗi class dưới đây tương ứng 1-1 với 1 "action" đã đặc tả trong
// docs/kien-truc-chi-tiet.md mục 2. Số thứ tự comment khớp với số mục trong tài liệu đó,
// để dễ tra ngược lại khi cần xem giải thích chi tiết.

/// <summary>Dùng cho các action có payload rỗng: GET_PROCESS_LIST, ENABLE_KEYLOGGER,
/// DISABLE_KEYLOGGER, STOP_SCREEN_VIEW, START_WEBCAM, STOP_WEBCAM, PING, PONG.</summary>
public class EmptyPayload { }

// ===== 2.1 Đăng ký & Pairing =====

public class RegisterAgentPayload
{
    public string AgentSecretKey { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty; // "Windows" | "MacOS"
    public string Hostname { get; set; } = string.Empty;
    public string? AgentVersion { get; set; }
}

public class RegisterAgentResultPayload
{
    public bool Success { get; set; }
    public string? AgentId { get; set; }
    public string? Message { get; set; }
}

public class RequestPairingPayload
{
    public string AgentId { get; set; } = string.Empty;
    public string Pin { get; set; } = string.Empty;
}

public class PairingResultPayload
{
    public bool Success { get; set; }
    public string? SessionId { get; set; }
    public string? Message { get; set; }
}

// ===== 2.2 Process Management =====

public class ProcessInfo
{
    public int Pid { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Path { get; set; }
    public double? CpuPercent { get; set; }
    public long? MemoryMB { get; set; }
}

public class ProcessListResultPayload
{
    public List<ProcessInfo> Processes { get; set; } = new();
}

public class StartProcessPayload
{
    public string TargetPath { get; set; } = string.Empty;
}

public class StartProcessResultPayload
{
    public bool Success { get; set; }
    public int? Pid { get; set; }
    public string? Message { get; set; }
}

public class StopProcessPayload
{
    public int Pid { get; set; }
}

public class StopProcessResultPayload
{
    public bool Success { get; set; }
    public string? Message { get; set; }
}

// ===== 2.3 Shutdown / Restart =====

public class ShutdownPayload
{
    public string ConfirmationToken { get; set; } = string.Empty;
    public int DelaySeconds { get; set; } = 10;
}

public class ShutdownResultPayload
{
    public bool Success { get; set; }
    public string? Message { get; set; }
}

// ===== 2.4 Keylogger =====

public class KeyloggerConsentResultPayload
{
    public bool Accepted { get; set; }
    public DateTime RespondedAt { get; set; }
}

public class KeylogEntry
{
    public string Text { get; set; } = string.Empty;
    public string? WindowTitle { get; set; }
    public DateTime Timestamp { get; set; }
}

public class KeylogBatchPayload
{
    public List<KeylogEntry> Entries { get; set; } = new();
}

public class DisableKeyloggerResultPayload
{
    public bool Success { get; set; }
}

// ===== 2.5 Screen View & Webcam =====

public class StartScreenViewPayload
{
    public string Quality { get; set; } = "medium"; // low | medium | high
    public int? IntervalMs { get; set; }
}

public class ScreenFramePayload
{
    public string DataBase64 { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public long FrameIndex { get; set; }
    public DateTime CapturedAt { get; set; }
}

public class WebcamFramePayload
{
    public string DataBase64 { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public long FrameIndex { get; set; }
    public DateTime CapturedAt { get; set; }
}

// ===== 2.6 File Browser & Transfer =====

public class ListDirPayload
{
    public string Path { get; set; } = string.Empty;
}

public class DirEntry
{
    public string Name { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public long? SizeBytes { get; set; }
    public DateTime? ModifiedAt { get; set; }
}

public class ListDirResultPayload
{
    public string Path { get; set; } = string.Empty;
    public List<DirEntry> Entries { get; set; } = new();
}

public class DownloadFilePayload
{
    public string Path { get; set; } = string.Empty;
    public string TransferId { get; set; } = string.Empty;
}

public class FileChunkPayload
{
    public string TransferId { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public int TotalChunks { get; set; }
    public string DataBase64 { get; set; } = string.Empty;
}

public class FileTransferCompletePayload
{
    public string TransferId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Sha256 { get; set; }
    public string? Message { get; set; }
}

public class UploadFileInitPayload
{
    public string TransferId { get; set; } = string.Empty;
    public string TargetPath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public int TotalChunks { get; set; }
    public string Sha256 { get; set; } = string.Empty;
}

public class UploadFileInitResultPayload
{
    public string TransferId { get; set; } = string.Empty;
    public bool Accepted { get; set; }
    public string? Message { get; set; }
}

public class UploadFileChunkPayload
{
    public string TransferId { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public string DataBase64 { get; set; } = string.Empty;
}

public class UploadFileResultPayload
{
    public string TransferId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Message { get; set; }
}

// ===== 2.7 Connection lifecycle =====

public class AgentDisconnectedPayload
{
    public string AgentId { get; set; } = string.Empty;
    public DateTime LastSeenAt { get; set; }
}

public class AgentReconnectedPayload
{
    public string AgentId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
}

// ===== 3. Mã lỗi chung (action ERROR) =====

public class ErrorPayload
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? RelatedAction { get; set; }
}
