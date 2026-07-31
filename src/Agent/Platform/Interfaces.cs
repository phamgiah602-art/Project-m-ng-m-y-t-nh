namespace RemoteControlLAN.Agent.Platform;

public sealed record CapturedImage(byte[] Bytes, int Width, int Height);
public sealed record KeyEvent(string Text, string? WindowTitle, DateTime Timestamp);
public interface IScreenCaptureService { Task<CapturedImage> CaptureJpegAsync(string quality, CancellationToken cancellationToken); }
public interface IWebcamCaptureService : IDisposable { Task<CapturedImage> CaptureJpegAsync(CancellationToken cancellationToken); }
public interface IKeyboardHookService : IDisposable { void Start(Action<KeyEvent> onKeyEvent); void Stop(); }
public interface IShutdownService { Task ShutdownAsync(int delaySeconds, CancellationToken cancellationToken); Task RestartAsync(int delaySeconds, CancellationToken cancellationToken); }
public interface IAppLauncherService { Task<int?> StartAsync(string targetPath, CancellationToken cancellationToken); }
public interface INotificationService { Task ShowAsync(string title, string message, CancellationToken cancellationToken); Task<bool> RequestConsentAsync(string message, CancellationToken cancellationToken); }
