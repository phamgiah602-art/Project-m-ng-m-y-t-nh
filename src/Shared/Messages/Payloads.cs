namespace RemoteControlLAN.Shared.Messages;

public sealed class EmptyPayload { }
public sealed class RegisterAgentPayload { public string AgentSecretKey { get; set; } = string.Empty; public string Platform { get; set; } = string.Empty; public string Hostname { get; set; } = string.Empty; public string? AgentVersion { get; set; } }
public sealed class RegisterAgentResultPayload { public bool Success { get; set; } public string? AgentId { get; set; } public string? Message { get; set; } }
public sealed class RequestPairingPayload { public string AgentId { get; set; } = string.Empty; public string Pin { get; set; } = string.Empty; }
public sealed class PairingResultPayload { public bool Success { get; set; } public string? SessionId { get; set; } public string? Message { get; set; } }
public sealed class PinPayload { public string Pin { get; set; } = string.Empty; }
public sealed class ProcessInfo { public int Pid { get; set; } public string Name { get; set; } = string.Empty; public string? Path { get; set; } public double? CpuPercent { get; set; } public long? MemoryMB { get; set; } }
public sealed class ProcessListResultPayload { public List<ProcessInfo> Processes { get; set; } = []; }
public sealed class StartProcessPayload { public string TargetPath { get; set; } = string.Empty; }
public sealed class StartProcessResultPayload { public bool Success { get; set; } public int? Pid { get; set; } public string? Message { get; set; } }
public sealed class StopProcessPayload { public int Pid { get; set; } }
public sealed class StopProcessResultPayload { public bool Success { get; set; } public string? Message { get; set; } }
public sealed class ShutdownPayload { public string ConfirmationToken { get; set; } = string.Empty; public int DelaySeconds { get; set; } = 10; }
public sealed class ShutdownResultPayload { public bool Success { get; set; } public string? Message { get; set; } }
public sealed class KeyloggerConsentResultPayload { public bool Accepted { get; set; } public DateTime RespondedAt { get; set; } }
public sealed class KeylogEntry { public string Text { get; set; } = string.Empty; public string? WindowTitle { get; set; } public DateTime Timestamp { get; set; } }
public sealed class KeylogBatchPayload { public List<KeylogEntry> Entries { get; set; } = []; }
public sealed class DisableKeyloggerResultPayload { public bool Success { get; set; } }
public sealed class StartScreenViewPayload { public string Quality { get; set; } = "medium"; public int? IntervalMs { get; set; } }
public sealed class ScreenFramePayload { public string DataBase64 { get; set; } = string.Empty; public int Width { get; set; } public int Height { get; set; } public long FrameIndex { get; set; } public DateTime CapturedAt { get; set; } }
public sealed class WebcamFramePayload { public string DataBase64 { get; set; } = string.Empty; public int Width { get; set; } public int Height { get; set; } public long FrameIndex { get; set; } public DateTime CapturedAt { get; set; } }
public sealed class ListDirPayload { public string Path { get; set; } = string.Empty; }
public sealed class DirEntry { public string Name { get; set; } = string.Empty; public bool IsDirectory { get; set; } public long? SizeBytes { get; set; } public DateTime? ModifiedAt { get; set; } }
public sealed class ListDirResultPayload { public string Path { get; set; } = string.Empty; public List<DirEntry> Entries { get; set; } = []; }
public sealed class DownloadFilePayload { public string Path { get; set; } = string.Empty; public string TransferId { get; set; } = string.Empty; }
public sealed class FileChunkPayload { public string TransferId { get; set; } = string.Empty; public int ChunkIndex { get; set; } public int TotalChunks { get; set; } public string DataBase64 { get; set; } = string.Empty; }
public sealed class FileTransferCompletePayload { public string TransferId { get; set; } = string.Empty; public bool Success { get; set; } public string? Sha256 { get; set; } public string? Message { get; set; } }
public sealed class UploadFileInitPayload { public string TransferId { get; set; } = string.Empty; public string TargetPath { get; set; } = string.Empty; public string FileName { get; set; } = string.Empty; public int TotalChunks { get; set; } public string Sha256 { get; set; } = string.Empty; }
public sealed class UploadFileInitResultPayload { public string TransferId { get; set; } = string.Empty; public bool Accepted { get; set; } public string? Message { get; set; } }
public sealed class UploadFileChunkPayload { public string TransferId { get; set; } = string.Empty; public int ChunkIndex { get; set; } public string DataBase64 { get; set; } = string.Empty; }
public sealed class UploadFileResultPayload { public string TransferId { get; set; } = string.Empty; public bool Success { get; set; } public string? Message { get; set; } }
public sealed class AgentDisconnectedPayload { public string AgentId { get; set; } = string.Empty; public DateTime LastSeenAt { get; set; } }
public sealed class SessionEndedPayload { public string? Message { get; set; } }
public sealed class ErrorPayload { public string Code { get; set; } = string.Empty; public string Message { get; set; } = string.Empty; public string? RelatedAction { get; set; } }
