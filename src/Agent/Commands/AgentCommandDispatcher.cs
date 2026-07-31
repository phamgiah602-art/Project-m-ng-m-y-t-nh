using System.Diagnostics;
using System.Text.Json;
using RemoteControlLAN.Agent.Configuration;
using RemoteControlLAN.Agent.Platform;
using RemoteControlLAN.Agent.Security;
using RemoteControlLAN.Agent.Transfers;
using RemoteControlLAN.Shared.Messages;

namespace RemoteControlLAN.Agent.Commands;

public sealed class AgentCommandDispatcher(AgentOptions options, PathGuard paths, ProcessGuard processes, IScreenCaptureService screen, IWebcamCaptureService webcam, IKeyboardHookService keyboard, IShutdownService power, IAppLauncherService launcher, INotificationService notifications, FileTransferService transfers)
{
    private CancellationTokenSource? _screenCts;
    private CancellationTokenSource? _webcamCts;
    public async Task ExecuteAsync(MessageEnvelope message, Func<MessageEnvelope, Task> send, CancellationToken cancellationToken)
    {
        try
        {
            switch (message.Action)
            {
                case "GET_PROCESS_LIST": await SendProcessListAsync(message, send); break;
                case "START_PROCESS": await StartProcessAsync(message, send, cancellationToken); break;
                case "STOP_PROCESS": await StopProcessAsync(message, send); break;
                case "SHUTDOWN": await PowerAsync(message, send, false, cancellationToken); break;
                case "RESTART": await PowerAsync(message, send, true, cancellationToken); break;
                case "ENABLE_KEYLOGGER": await EnableKeyloggerAsync(message, send, cancellationToken); break;
                case "DISABLE_KEYLOGGER": keyboard.Stop(); await send(Response(message, "DISABLE_KEYLOGGER_RESULT", new DisableKeyloggerResultPayload { Success = true })); break;
                case "START_SCREEN_VIEW": StartScreen(message, send, cancellationToken); break;
                case "STOP_SCREEN_VIEW": _screenCts?.Cancel(); break;
                case "START_WEBCAM": StartWebcam(message, send, cancellationToken); break;
                case "STOP_WEBCAM": _webcamCts?.Cancel(); break;
                case "LIST_DIR": await ListDirectoryAsync(message, send); break;
                case "DOWNLOAD_FILE": await DownloadAsync(message, send, cancellationToken); break;
                case "UPLOAD_FILE_INIT": await UploadInitAsync(message, send); break;
                case "UPLOAD_FILE_CHUNK": await UploadChunkAsync(message, send, cancellationToken); break;
                default: await ErrorAsync(message, send, "UNKNOWN_ACTION", "Agent không hỗ trợ action này."); break;
            }
        }
        catch (UnauthorizedAccessException ex) { await ErrorAsync(message, send, message.Action is "STOP_PROCESS" ? "PROCESS_PROTECTED" : "PATH_BLOCKED", ex.Message); }
        catch (Exception ex) { await ErrorAsync(message, send, ex is UnauthorizedAccessException ? "PERMISSION_DENIED" : "COMMAND_FAILED", ex.Message); }
    }
    private static MessageEnvelope Response(MessageEnvelope source, string action, object payload) => MessageEnvelope.Create("RESPONSE", action, payload, source.SessionId, source.AgentId);
    private static Task ErrorAsync(MessageEnvelope source, Func<MessageEnvelope, Task> send, string code, string message) => send(Response(source, "ERROR", new ErrorPayload { Code = code, Message = message, RelatedAction = source.Action }));
    private static async Task SendProcessListAsync(MessageEnvelope source, Func<MessageEnvelope, Task> send)
    {
        var list = Process.GetProcesses().OrderBy(p => p.ProcessName).Select(p => { try { return new ProcessInfo { Pid = p.Id, Name = p.ProcessName, Path = TryPath(p), MemoryMB = p.WorkingSet64 / 1024 / 1024 }; } finally { p.Dispose(); } }).ToList();
        await send(Response(source, "PROCESS_LIST_RESULT", new ProcessListResultPayload { Processes = list }));
    }
    private async Task StartProcessAsync(MessageEnvelope source, Func<MessageEnvelope, Task> send, CancellationToken ct) { var path = paths.ResolveAllowedPath(source.GetPayload<StartProcessPayload>()?.TargetPath ?? string.Empty); var pid = await launcher.StartAsync(path, ct); await send(Response(source, "START_PROCESS_RESULT", new StartProcessResultPayload { Success = true, Pid = pid, Message = "Đã khởi động ứng dụng." })); }
    private async Task StopProcessAsync(MessageEnvelope source, Func<MessageEnvelope, Task> send) { var pid = source.GetPayload<StopProcessPayload>()?.Pid ?? 0; using var process = Process.GetProcessById(pid); processes.EnsureCanStop(process); process.Kill(true); await send(Response(source, "STOP_PROCESS_RESULT", new StopProcessResultPayload { Success = true, Message = "Đã dừng tiến trình." })); }
    private async Task PowerAsync(MessageEnvelope source, Func<MessageEnvelope, Task> send, bool restart, CancellationToken ct) { if (!options.AllowPowerCommands) throw new UnauthorizedAccessException("Lệnh nguồn đang bị tắt trong cấu hình Agent."); var delay = source.GetPayload<ShutdownPayload>()?.DelaySeconds ?? 10; if (restart) await power.RestartAsync(delay, ct); else await power.ShutdownAsync(delay, ct); await send(Response(source, restart ? "RESTART_RESULT" : "SHUTDOWN_RESULT", new ShutdownResultPayload { Success = true, Message = "Lệnh nguồn đã được gửi tới hệ điều hành." })); }
    private async Task EnableKeyloggerAsync(MessageEnvelope source, Func<MessageEnvelope, Task> send, CancellationToken ct) { var accepted = await notifications.RequestConsentAsync("Operator muốn bật ghi nhận bàn phím. Bạn có đồng ý không?", ct); await send(Response(source, "KEYLOGGER_CONSENT_RESULT", new KeyloggerConsentResultPayload { Accepted = accepted, RespondedAt = DateTime.UtcNow })); if (accepted) { keyboard.Start(entry => _ = send(MessageEnvelope.Create("STREAM_FRAME", "KEYLOG_BATCH", new KeylogBatchPayload { Entries = [new KeylogEntry { Text = entry.Text, WindowTitle = entry.WindowTitle, Timestamp = entry.Timestamp }] }, source.SessionId, source.AgentId))); await notifications.ShowAsync("Remote Control LAN", "Ghi nhận bàn phím đang hoạt động.", ct); } }
    private void StartScreen(MessageEnvelope source, Func<MessageEnvelope, Task> send, CancellationToken outerCt) { _screenCts?.Cancel(); _screenCts = CancellationTokenSource.CreateLinkedTokenSource(outerCt); var request = source.GetPayload<StartScreenViewPayload>() ?? new(); _ = StreamScreenAsync(source, request, send, _screenCts.Token); }
    private async Task StreamScreenAsync(MessageEnvelope source, StartScreenViewPayload request, Func<MessageEnvelope, Task> send, CancellationToken ct) { var index = 0L; var interval = request.IntervalMs ?? request.Quality switch { "low" => 800, "high" => 250, _ => 400 }; while (!ct.IsCancellationRequested) { var image = await screen.CaptureJpegAsync(request.Quality, ct); await send(MessageEnvelope.Create("STREAM_FRAME", "SCREEN_FRAME", new ScreenFramePayload { DataBase64 = Convert.ToBase64String(image.Bytes), Width = image.Width, Height = image.Height, FrameIndex = index++, CapturedAt = DateTime.UtcNow }, source.SessionId, source.AgentId)); await Task.Delay(Math.Clamp(interval, 200, 2000), ct); } }
    private void StartWebcam(MessageEnvelope source, Func<MessageEnvelope, Task> send, CancellationToken outerCt) { _webcamCts?.Cancel(); _webcamCts = CancellationTokenSource.CreateLinkedTokenSource(outerCt); _ = StreamWebcamAsync(source, send, _webcamCts.Token); }
    private async Task StreamWebcamAsync(MessageEnvelope source, Func<MessageEnvelope, Task> send, CancellationToken ct) { await notifications.ShowAsync("Remote Control LAN", "Webcam đang được truy cập.", ct); var index = 0L; while (!ct.IsCancellationRequested) { var image = await webcam.CaptureJpegAsync(ct); await send(MessageEnvelope.Create("STREAM_FRAME", "WEBCAM_FRAME", new WebcamFramePayload { DataBase64 = Convert.ToBase64String(image.Bytes), Width = image.Width, Height = image.Height, FrameIndex = index++, CapturedAt = DateTime.UtcNow }, source.SessionId, source.AgentId)); await Task.Delay(300, ct); } }
    private async Task ListDirectoryAsync(MessageEnvelope source, Func<MessageEnvelope, Task> send) { var path = paths.ResolveAllowedPath(source.GetPayload<ListDirPayload>()?.Path ?? string.Empty); var entries = Directory.EnumerateFileSystemEntries(path).Select(p => { var attributes = File.GetAttributes(p); var isDirectory = attributes.HasFlag(FileAttributes.Directory); var info = isDirectory ? null : new FileInfo(p); return new DirEntry { Name = Path.GetFileName(p), IsDirectory = isDirectory, SizeBytes = info?.Length, ModifiedAt = isDirectory ? Directory.GetLastWriteTimeUtc(p) : info?.LastWriteTimeUtc }; }).OrderByDescending(x => x.IsDirectory).ThenBy(x => x.Name).ToList(); await send(Response(source, "LIST_DIR_RESULT", new ListDirResultPayload { Path = path, Entries = entries })); }
    private async Task DownloadAsync(MessageEnvelope source, Func<MessageEnvelope, Task> send, CancellationToken ct) { var request = source.GetPayload<DownloadFilePayload>() ?? throw new InvalidOperationException("Payload download không hợp lệ."); await foreach (var result in transfers.DownloadAsync(request, source.SessionId ?? string.Empty, source.AgentId ?? string.Empty, ct)) await send(result); }
    private async Task UploadInitAsync(MessageEnvelope source, Func<MessageEnvelope, Task> send) { var result = await transfers.BeginUploadAsync(source.GetPayload<UploadFileInitPayload>() ?? throw new InvalidOperationException("Payload upload không hợp lệ.")); await send(Response(source, "UPLOAD_FILE_INIT_RESULT", result)); }
    private async Task UploadChunkAsync(MessageEnvelope source, Func<MessageEnvelope, Task> send, CancellationToken ct) { var result = await transfers.WriteChunkAsync(source.GetPayload<UploadFileChunkPayload>() ?? throw new InvalidOperationException("Payload chunk không hợp lệ."), ct); if (result is not null) await send(Response(source, "UPLOAD_FILE_RESULT", result)); }
    private static string? TryPath(Process process) { try { return process.MainModule?.FileName; } catch { return null; } }
}
