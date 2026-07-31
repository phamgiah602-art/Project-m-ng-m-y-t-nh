using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using OpenCvSharp;

namespace RemoteControlLAN.Agent.Platform;

public sealed class PlatformServiceFactory
{
    public (IScreenCaptureService Screen, IWebcamCaptureService Webcam, IKeyboardHookService Keyboard, IShutdownService Power, IAppLauncherService Launcher, INotificationService Notifications) Create() =>
        (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? new WindowsScreenCaptureService() : new MacScreenCaptureService(), new OpenCvWebcamCaptureService(), new ConsentKeyboardHookService(), new PlatformShutdownService(), new PlatformAppLauncherService(), new NativeNotificationService());
}

public sealed class WindowsScreenCaptureService : IScreenCaptureService
{
    public Task<CapturedImage> CaptureJpegAsync(string quality, CancellationToken cancellationToken) => Task.Run(() =>
    {
        var width = GetSystemMetrics(0); var height = GetSystemMetrics(1); if (width <= 0 || height <= 0) throw new InvalidOperationException("Không tìm thấy màn hình.");
        using var bitmap = new Bitmap(width, height); using (var graphics = Graphics.FromImage(bitmap)) graphics.CopyFromScreen(0, 0, 0, 0, bitmap.Size);
        return Compress(bitmap, quality);
    }, cancellationToken);
    internal static CapturedImage Compress(Bitmap bitmap, string quality)
    {
        const int maxWidth = 1280; var scale = Math.Min(1d, maxWidth / (double)bitmap.Width); var width = (int)(bitmap.Width * scale); var height = (int)(bitmap.Height * scale);
        using var resized = new Bitmap(bitmap, width, height); using var stream = new MemoryStream(); var codec = ImageCodecInfo.GetImageEncoders().Single(x => x.FormatID == ImageFormat.Jpeg.Guid); using var parameters = new EncoderParameters(1); parameters.Param[0] = new EncoderParameter(Encoder.Quality, quality switch { "low" => 45L, "high" => 75L, _ => 60L }); resized.Save(stream, codec, parameters); return new(stream.ToArray(), width, height);
    }
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
}

public sealed class MacScreenCaptureService : IScreenCaptureService
{
    public async Task<CapturedImage> CaptureJpegAsync(string quality, CancellationToken cancellationToken)
    {
        var path = Path.Combine(Path.GetTempPath(), $"rclan-{Guid.NewGuid():N}.jpg");
        try { var process = Process.Start(new ProcessStartInfo("screencapture", $"-x -t jpg \"{path}\"") { UseShellExecute = false }) ?? throw new InvalidOperationException("Không thể gọi screencapture."); await process.WaitForExitAsync(cancellationToken); if (process.ExitCode != 0) throw new UnauthorizedAccessException("macOS chưa cấp quyền Screen Recording."); using var bitmap = new Bitmap(path); return WindowsScreenCaptureService.Compress(bitmap, quality); }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}

public sealed class OpenCvWebcamCaptureService : IWebcamCaptureService
{
    private readonly VideoCapture _capture = new(0);
    public Task<CapturedImage> CaptureJpegAsync(CancellationToken cancellationToken) => Task.Run(() => { if (!_capture.IsOpened()) throw new UnauthorizedAccessException("Không thể mở camera; hãy kiểm tra quyền Camera."); using var frame = new Mat(); if (!_capture.Read(frame) || frame.Empty()) throw new InvalidOperationException("Không đọc được frame webcam."); Cv2.ImEncode(".jpg", frame, out var bytes, [new ImageEncodingParam(ImwriteFlags.JpegQuality, 60)]); return new CapturedImage(bytes, frame.Width, frame.Height); }, cancellationToken);
    public void Dispose() => _capture.Dispose();
}

public sealed class ConsentKeyboardHookService : IKeyboardHookService
{
    private Action<KeyEvent>? _onKeyEvent;
    private Thread? _hookThread;
    private CancellationTokenSource? _cts;

    public void Start(Action<KeyEvent> onKeyEvent)
    {
        Stop();
        _onKeyEvent = onKeyEvent;
        _cts = new CancellationTokenSource();
        _hookThread = new Thread(HookLoop) { IsBackground = true };
        _hookThread.Start();
    }

    public void Stop()
    {
        _cts?.Cancel();
        if (_hookThread != null)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                PostQuitMessage(0);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                StopMacRunLoop();
            }
            _hookThread.Join(1000);
            _hookThread = null;
        }
        _cts?.Dispose();
        _cts = null;
    }

    public void Dispose()
    {
        Stop();
    }

    private void HookLoop()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _proc = HookCallback;
            using var curProcess = Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule;
            var hMod = curModule != null ? GetModuleHandle(curModule.ModuleName) : IntPtr.Zero;
            _hookId = SetWindowsHookEx(13, _proc, hMod, 0);
            if (_hookId == IntPtr.Zero) return;

            try
            {
                MSG msg;
                while (GetMessage(out msg, IntPtr.Zero, 0, 0) != 0 && !_cts!.Token.IsCancellationRequested)
                {
                    TranslateMessage(ref msg);
                    DispatchMessage(ref msg);
                }
            }
            finally
            {
                UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            MacHookLoop();
        }
    }

    // Windows P/Invoke
    private IntPtr _hookId = IntPtr.Zero;
    private LowLevelKeyboardProc? _proc;

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr DispatchMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    private static extern bool GetKeyboardState(byte[] lpKeyboardState);

    [DllImport("user32.dll")]
    private static extern int ToUnicode(uint wVirtKey, uint wScanCode, byte[] lpKeyboardState, [Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pwszBuff, int cchBuff, uint wFlags);

    [DllImport("user32.dll")]
    private static extern void PostQuitMessage(int nExitCode);

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
        public uint lPrivate;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == (IntPtr)0x0100 || wParam == (IntPtr)0x0104)) // WM_KEYDOWN, WM_SYSKEYDOWN
        {
            var kb = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            var text = GetKeyTextWin(kb.vkCode, kb.scanCode, kb.flags);
            if (!string.IsNullOrEmpty(text))
            {
                var title = GetActiveWindowTitleWin();
                _onKeyEvent?.Invoke(new KeyEvent(text, title, DateTime.UtcNow));
            }
        }
        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private static string GetActiveWindowTitleWin()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return "Unknown";
        var sb = new System.Text.StringBuilder(256);
        if (GetWindowText(hwnd, sb, sb.Capacity) > 0)
        {
            return sb.ToString();
        }
        return "Unknown";
    }

    private static string GetKeyTextWin(uint vkCode, uint scanCode, uint flags)
    {
        switch (vkCode)
        {
            case 8: return "[Backspace]";
            case 9: return "[Tab]";
            case 13: return "[Enter]\n";
            case 27: return "[Esc]";
            case 32: return " ";
            case 46: return "[Delete]";
        }

        var keyboardState = new byte[256];
        if ((GetAsyncKeyState(16) & 0x8000) != 0) keyboardState[16] = 0x80; // Shift
        if ((GetAsyncKeyState(20) & 0x0001) != 0) keyboardState[20] = 0x01; // Caps Lock
        
        var sb = new System.Text.StringBuilder(5);
        var result = ToUnicode(vkCode, scanCode, keyboardState, sb, sb.Capacity, 0);
        if (result > 0) return sb.ToString();
        return "";
    }

    // Mac P/Invoke
    private IntPtr _runLoop = IntPtr.Zero;
    private IntPtr _eventTap = IntPtr.Zero;
    private CGEventTapCallBack? _macCallback;

    private delegate IntPtr CGEventTapCallBack(IntPtr proxy, int type, IntPtr @event, IntPtr refcon);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern IntPtr CGEventTapCreate(
        int tap,
        int place,
        int options,
        ulong eventsOfInterest,
        CGEventTapCallBack callback,
        IntPtr userInfo);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern IntPtr CFMachPortCreateRunLoopSource(IntPtr allocator, IntPtr port, int order);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern IntPtr CFRunLoopGetCurrent();

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFRunLoopAddSource(IntPtr rl, IntPtr source, IntPtr mode);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFRunLoopRun();

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFRunLoopStop(IntPtr rl);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern int CGEventGetIntegerValueField(IntPtr @event, int field);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern ushort CGEventGetFlags(IntPtr @event);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern IntPtr CFStringCreateWithCString(IntPtr alloc, string str, uint encoding);

    private const int kCGKeyboardEventKeycode = 9;

    private void MacHookLoop()
    {
        _macCallback = MacEventTapCallback;
        ulong eventMask = 1UL << 10; // kCGEventKeyDown = 10 -> mask = 1UL << 10
        _eventTap = CGEventTapCreate(0, 1, 1, eventMask, _macCallback, IntPtr.Zero);
        if (_eventTap == IntPtr.Zero)
        {
            Console.WriteLine("[KEYLOGGER] Không tạo được event tap. Hãy cấp quyền Accessibility.");
            return;
        }

        var runLoopSource = CFMachPortCreateRunLoopSource(IntPtr.Zero, _eventTap, 0);
        if (runLoopSource == IntPtr.Zero) return;

        _runLoop = CFRunLoopGetCurrent();
        CFRunLoopAddSource(_runLoop, runLoopSource, GetCFRunLoopCommonModes());
        CFRunLoopRun();
    }

    private static IntPtr _commonModes = IntPtr.Zero;
    private static IntPtr GetCFRunLoopCommonModes()
    {
        if (_commonModes == IntPtr.Zero)
        {
            _commonModes = CFStringCreateWithCString(IntPtr.Zero, "kCFRunLoopDefaultMode", 0x08000100);
        }
        return _commonModes;
    }

    private void StopMacRunLoop()
    {
        if (_runLoop != IntPtr.Zero)
        {
            CFRunLoopStop(_runLoop);
            _runLoop = IntPtr.Zero;
        }
    }

    private IntPtr MacEventTapCallback(IntPtr proxy, int type, IntPtr @event, IntPtr refcon)
    {
        if (type == 10) // kCGEventKeyDown
        {
            var keyCode = (ushort)CGEventGetIntegerValueField(@event, kCGKeyboardEventKeycode);
            var flags = CGEventGetFlags(@event);
            bool shift = (flags & 0x00020000) != 0;
            bool caps = (flags & 0x00010000) != 0;
            
            var text = MapMacKeyCode(keyCode, shift, caps);
            if (!string.IsNullOrEmpty(text))
            {
                var title = GetActiveWindowTitleMac();
                _onKeyEvent?.Invoke(new KeyEvent(text, title, DateTime.UtcNow));
            }
        }
        return @event;
    }

    private static string _cachedTitle = "Unknown";
    private static DateTime _lastTitleUpdate = DateTime.MinValue;
    private static string GetActiveWindowTitleMac()
    {
        if ((DateTime.UtcNow - _lastTitleUpdate).TotalSeconds < 2) return _cachedTitle;
        _lastTitleUpdate = DateTime.UtcNow;
        Task.Run(() =>
        {
            try
            {
                var psi = new ProcessStartInfo("osascript", "-e \"tell application \\\"System Events\\\" to get name of first process whose frontmost is true\"")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi);
                if (p != null)
                {
                    p.WaitForExit(500);
                    var name = p.StandardOutput.ReadToEnd().Trim();
                    if (!string.IsNullOrEmpty(name)) _cachedTitle = name;
                }
            }
            catch { }
        });
        return _cachedTitle;
    }

    private static string MapMacKeyCode(ushort keyCode, bool shift, bool caps)
    {
        switch (keyCode)
        {
            case 0: return shift || caps ? "A" : "a";
            case 11: return shift || caps ? "B" : "b";
            case 8: return shift || caps ? "C" : "c";
            case 2: return shift || caps ? "D" : "d";
            case 14: return shift || caps ? "E" : "e";
            case 3: return shift || caps ? "F" : "f";
            case 5: return shift || caps ? "G" : "g";
            case 4: return shift || caps ? "H" : "h";
            case 34: return shift || caps ? "I" : "i";
            case 38: return shift || caps ? "J" : "j";
            case 40: return shift || caps ? "K" : "k";
            case 37: return shift || caps ? "L" : "l";
            case 46: return shift || caps ? "M" : "m";
            case 45: return shift || caps ? "N" : "n";
            case 31: return shift || caps ? "O" : "o";
            case 35: return shift || caps ? "P" : "p";
            case 12: return shift || caps ? "Q" : "q";
            case 15: return shift || caps ? "R" : "r";
            case 1: return shift || caps ? "S" : "s";
            case 17: return shift || caps ? "T" : "t";
            case 32: return shift || caps ? "U" : "u";
            case 9: return shift || caps ? "V" : "v";
            case 13: return shift || caps ? "W" : "w";
            case 7: return shift || caps ? "X" : "x";
            case 16: return shift || caps ? "Y" : "y";
            case 6: return shift || caps ? "Z" : "z";
            
            case 18: return shift ? "!" : "1";
            case 19: return shift ? "@" : "2";
            case 20: return shift ? "#" : "3";
            case 21: return shift ? "$" : "4";
            case 23: return shift ? "%" : "5";
            case 22: return shift ? "^" : "6";
            case 26: return shift ? "&" : "7";
            case 28: return shift ? "*" : "8";
            case 25: return shift ? "(" : "9";
            case 29: return shift ? ")" : "0";

            case 49: return " ";
            case 36: return "[Enter]\n";
            case 51: return "[Backspace]";
            case 48: return "[Tab]";
            case 53: return "[Esc]";
            
            case 43: return shift ? "<" : ",";
            case 47: return shift ? ">" : ".";
            case 44: return shift ? "?" : "/";
            case 41: return shift ? ":" : ";";
            case 39: return shift ? "\"" : "'";
            case 33: return shift ? "{" : "[";
            case 30: return shift ? "}" : "]";
            case 42: return shift ? "|" : "\\";
            case 24: return shift ? "+" : "=";
            case 27: return shift ? "_" : "-";
            
            default: return "";
        }
    }
}

public sealed class PlatformShutdownService : IShutdownService
{
    public Task ShutdownAsync(int delaySeconds, CancellationToken cancellationToken) => RunAsync(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "shutdown" : "osascript", RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? $"/s /t {Math.Clamp(delaySeconds, 0, 3600)}" : "-e 'tell application \"System Events\" to shut down'", cancellationToken);
    public Task RestartAsync(int delaySeconds, CancellationToken cancellationToken) => RunAsync(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "shutdown" : "osascript", RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? $"/r /t {Math.Clamp(delaySeconds, 0, 3600)}" : "-e 'tell application \"System Events\" to restart'", cancellationToken);
    private static async Task RunAsync(string fileName, string args, CancellationToken ct) { var process = Process.Start(new ProcessStartInfo(fileName, args) { UseShellExecute = false }) ?? throw new InvalidOperationException("Không khởi động được lệnh nguồn."); await process.WaitForExitAsync(ct); }
}

public sealed class PlatformAppLauncherService : IAppLauncherService
{
    public Task<int?> StartAsync(string targetPath, CancellationToken cancellationToken)
    {
        var startInfo = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? new ProcessStartInfo("open", $"-a \"{targetPath}\"") : new ProcessStartInfo(targetPath);
        startInfo.UseShellExecute = true; return Task.FromResult(Process.Start(startInfo)?.Id);
    }
}

public sealed class NativeNotificationService : INotificationService
{
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

    public Task ShowAsync(string title, string message, CancellationToken ct) => RunAppleScriptAsync($"display notification {Quote(message)} with title {Quote(title)}", ct);
    public async Task<bool> RequestConsentAsync(string message, CancellationToken ct)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) 
        { 
            var psi = new ProcessStartInfo("osascript") { UseShellExecute = false, RedirectStandardOutput = true };
            psi.ArgumentList.Add("-e");
            psi.ArgumentList.Add($"button returned of (display dialog {Quote(message)} buttons {{\"Từ chối\", \"Đồng ý\"}} default button \"Từ chối\")");
            var process = Process.Start(psi)!; 
            var output = await process.StandardOutput.ReadToEndAsync(ct); 
            await process.WaitForExitAsync(ct); 
            return process.ExitCode == 0 && output.Trim() == "Đồng ý"; 
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // MB_YESNO = 0x00000004, MB_ICONQUESTION = 0x00000020, IDYES = 6
            return MessageBox(IntPtr.Zero, message, "Remote Control LAN", 0x00000004 | 0x00000020) == 6;
        }
        Console.WriteLine($"[CONSENT REQUIRED] {message}. Nhấn Y rồi Enter để đồng ý."); return string.Equals(Console.ReadLine(), "Y", StringComparison.OrdinalIgnoreCase);
    }
    private static Task RunAppleScriptAsync(string script, CancellationToken ct) 
    { 
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) { Console.WriteLine(script); return Task.CompletedTask; } 
        var psi = new ProcessStartInfo("osascript") { UseShellExecute = false };
        psi.ArgumentList.Add("-e");
        psi.ArgumentList.Add(script);
        var process = Process.Start(psi); 
        return process is null ? Task.CompletedTask : process.WaitForExitAsync(ct); 
    }
    private static string Quote(string text) => "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}
