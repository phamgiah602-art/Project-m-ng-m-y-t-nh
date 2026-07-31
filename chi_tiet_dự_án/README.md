# Remote Control LAN — Web App Điều Khiển Máy Tính Từ Xa Trong Mạng LAN

> Đồ án môn Mạng Máy Tính (Lập trình ứng dụng mạng) — Nhóm 3 thành viên
> Tài liệu này là **đặc tả kỹ thuật đầy đủ**, dùng làm nguồn tham chiếu duy nhất (single source of truth) cho toàn bộ quá trình phát triển, kể cả khi triển khai bằng AI Coding Agent. Mọi quyết định kiến trúc, quy ước code, luồng dữ liệu đều phải bám theo tài liệu này. Nếu phát sinh yêu cầu mới hoặc mâu thuẫn với tài liệu, **phải cập nhật README trước, không tự ý đi lệch**.

---

## 1. Tổng quan dự án

Xây dựng một **Web App** cho phép một người dùng ("Operator") dùng trình duyệt để **kết nối và điều khiển từ xa** một máy tính khác ("Target") đang cùng kết nối trong **một mạng LAN** (ví dụ: các laptop học sinh trong phòng máy của trường).

Hệ thống gồm 3 thành phần:

| Thành phần | Vai trò | Chạy trên |
|---|---|---|
| **Web Client** | Giao diện điều khiển, do Operator sử dụng | Trình duyệt (bất kỳ máy nào trong LAN) |
| **Gateway Server** | Trung gian xác thực + relay toàn bộ dữ liệu giữa Web Client và Agent | 1 máy chủ trong LAN (Windows hoặc macOS đều chạy được, vì ASP.NET Core + SQLite đa nền tảng) |
| **Agent** | Chương trình cài trên máy bị điều khiển, thực thi lệnh và gửi dữ liệu về | Máy Target — hỗ trợ **cả Windows và macOS** (xem mục 4.1 — lớp trừu tượng nền tảng) |

**Nguyên tắc bất di bất dịch:** *toàn bộ dữ liệu giữa Web Client và Agent — kể cả lệnh điều khiển, hình ảnh màn hình, webcam, dữ liệu file, log bàn phím — đều đi qua Gateway Server bằng WebSocket.* Không có kết nối trực tiếp (peer-to-peer) giữa Web Client và Agent, để đảm bảo Gateway kiểm soát, xác thực và ghi log (audit) được toàn bộ hành vi điều khiển.

---

## 2. Nguyên tắc đạo đức & phạm vi sử dụng

Vì phần mềm này có khả năng giám sát bàn phím, xem màn hình/webcam, xóa/tắt máy — đây là các chức năng **nhạy cảm**, bắt buộc tuân thủ:

- Chỉ được cài Agent và sử dụng hệ thống này **trên các máy mà nhóm có quyền/được sự đồng ý sử dụng** (máy cá nhân của thành viên nhóm, hoặc máy phòng lab được giáo viên cho phép dùng để demo).
- Agent **không bao giờ được chạy ẩn hoàn toàn không dấu vết**: luôn có icon khay hệ thống (system tray) hiển thị trạng thái "đang được điều khiển / đang bị giám sát".
- Tính năng Keylogger bắt buộc có cơ chế thông báo & xin phép tại chỗ trên máy Target (chi tiết ở mục 6.3).
- Không public Gateway ra Internet ngoài phạm vi LAN của đồ án.

---

## 3. Kiến trúc hệ thống

```
┌─────────────────┐      Raw WebSocket (ws://)        ┌──────────────────┐      Raw WebSocket (ws://)        ┌─────────────────┐
│   Web Client     │ <───────────────────────────────> │  Gateway Server   │ <───────────────────────────────> │      Agent       │
│  (React + TS)    │        (ws://gateway:port)         │ (ASP.NET Core 8)  │        (ws://gateway:port)         │  (C# / .NET 8)   │
│  - Operator UI   │                                     │  - Auth (JWT)     │                                     │  - Platform Layer│
│  - Xem màn hình  │                                     │  - PIN Pairing    │                                     │    (Win/macOS)   │
│  - File browser  │                                     │  - Message Router │                                     │  - Screen Capture│
└─────────────────┘                                     │  - Audit Log (DB) │                                     │  - Webcam Capture│
                                                          └──────────────────┘                                     │  - Keylogger Hook│
                                                                                                                    │  - Process Ctrl  │
                                                                                                                    └─────────────────┘
```

- Gateway đóng vai trò **Mediator**: không có logic nghiệp vụ nặng, chỉ xác thực, ghép cặp (pairing), và **định tuyến (route)** message giữa đúng cặp Web Client ↔ Agent theo `SessionId`.
- Một Gateway có thể phục vụ nhiều cặp Operator–Target cùng lúc (nhiều session song song), miễn là mỗi session độc lập, không lẫn dữ liệu.
- Dùng **raw WebSocket** thuần túy (không qua SignalR) — đơn giản hơn nhưng nhóm phải tự cài đặt quản lý kết nối/reconnect (mục 4.2). Dùng **`ws://`** (không TLS) để đơn giản hóa, chấp nhận được vì hệ thống chỉ chạy trong LAN kín của đồ án (xem lưu ý bảo mật ở mục 9).

---

## 4. Tech Stack

| Lớp | Công nghệ | Ghi chú |
|---|---|---|
| Agent | **C# / .NET 8** (Console App / Background Service chạy nền — **không** dùng WinForms làm nền tảng chính vì cần chạy được trên cả Windows và macOS) | Cần quyền Admin/sudo cho một số thao tác (shutdown, keylogger hook, screen capture trên macOS) |
| Giao tiếp thời gian thực | **Raw WebSocket** — `System.Net.WebSockets` (`ClientWebSocket` phía Agent, `HttpContext.WebSockets` phía Gateway), trình duyệt dùng WebSocket API gốc | Theo yêu cầu dùng raw WebSocket thuần túy, không qua SignalR. Nhóm tự cài đặt: quản lý kết nối, heartbeat (ping/pong), cơ chế reconnect (xem mục 4.2) |
| Gateway Backend | **ASP.NET Core 8 Web API** (`app.UseWebSockets()` + middleware xử lý kết nối thủ công) | Cùng ngôn ngữ với Agent → dễ dùng chung Model/DTO |
| Database | **SQLite + Entity Framework Core** | Nhẹ, không cần cài server DB riêng, phù hợp đồ án |
| Auth | **ASP.NET Core Identity** (password hash) **+ JWT** cho session token | Không dùng OTP, dùng PIN pairing thay cho lớp xác thực thứ 2 |
| Nền tảng đích (Target) | **Windows và macOS** | Bắt buộc có lớp trừu tượng nền tảng cho Agent — xem mục 4.1 |
| Screen Capture | Windows: `System.Drawing.Graphics.CopyFromScreen` · macOS: gọi lệnh `screencapture` qua `Process.Start` | Nén JPEG trước khi gửi để giảm băng thông |
| Webcam Capture | **OpenCvSharp4** (dùng chung mục tiêu cho cả 2 OS) | ⚠️ Cần spike/thử nghiệm sớm để xác nhận hoạt động ổn định trên macOS — xem mục 16 |
| Keylogger | Windows: `SetWindowsHookEx` (P/Invoke `user32.dll`) · macOS: `CGEventTap` (P/Invoke `ApplicationServices`/`CoreGraphics`), cần user cấp quyền **Accessibility** | Bắt buộc kèm cơ chế xin phép — mục 6.3. Phần macOS phức tạp hơn đáng kể |
| Web Client | **React 18 + TypeScript + Vite** | Dùng `WebSocket` API gốc của trình duyệt, không cần thư viện ngoài |
| UI Styling | **TailwindCSS** | |
| Logging | **Serilog** (cả Gateway và Agent) | Ghi log ra file + phục vụ audit |

### 4.1 Lớp trừu tượng nền tảng (Platform Abstraction Layer) — bắt buộc vì Agent chạy trên cả Windows và macOS

Vì các API hệ thống (chụp màn hình, hook bàn phím, tắt máy, mở ứng dụng, thông báo) **khác nhau hoàn toàn** giữa Windows và macOS, Agent phải tách các thao tác này thành interface chung, mỗi OS có 1 implementation riêng — áp dụng **Factory Pattern + Strategy Pattern** (chi tiết mục 8):

```csharp
public interface IScreenCaptureService { Task<byte[]> CaptureJpegAsync(); }
public interface IWebcamCaptureService { Task<byte[]> CaptureJpegAsync(); }
public interface IKeyboardHookService { void Start(Action<KeyEvent> onKeyEvent); void Stop(); }
public interface IShutdownService { void Shutdown(); void Restart(); }
public interface IAppLauncherService { void StartApp(string path); IEnumerable<AppInfo> ListInstalledApps(); }
public interface INotificationService { void ShowNotification(string title, string message); bool ShowConsentDialog(string message); }
```

Tại `Program.cs` của Agent, dùng `RuntimeInformation.IsOSPlatform(OSPlatform.Windows)` (hoặc `OSPlatform.OSX`) để chọn implementation phù hợp thông qua 1 `PlatformServiceFactory` — phần còn lại của Agent (Command handlers, luồng gửi dữ liệu qua WebSocket) chỉ làm việc với các interface trên, **không quan tâm đang chạy OS nào**.

> **Quyết định đơn giản hóa (cần bạn xác nhận):** thay vì xây tray icon GUI đầy đủ (phức tạp, khác nhau nhiều giữa 2 OS), Agent sẽ chạy dạng **background console/service không có cửa sổ chính**, và dùng **thông báo hệ thống gốc (native notification)** để báo trạng thái + xin phép:
> - Windows: `NotifyIcon` (Windows Forms — chỉ compile/chạy nhánh này trên Windows) hoặc Toast Notification.
> - macOS: gọi `osascript -e 'display notification ...'` và `osascript -e 'display dialog ...'` cho popup xin phép (mục 6.3).
>
> Cách này đơn giản hơn nhiều so với xây UI đa nền tảng bằng Avalonia, phù hợp thời gian làm đồ án. Nếu nhóm muốn có tray icon "xịn" hơn, có thể nâng cấp sau — báo lại nếu muốn đổi.

### 4.2 Quản lý kết nối WebSocket thủ công (vì không dùng SignalR)

Vì dùng raw WebSocket, Gateway phải tự cài đặt các phần mà SignalR vốn làm sẵn:

- **Connection Manager**: `ConcurrentDictionary<Guid, WebSocket>` lưu toàn bộ kết nối đang mở (cả Agent lẫn Web Client), khóa bằng `ConnectionId`.
- **Heartbeat**: Gateway gửi ping (message JSON `{"type":"PING"}`) mỗi 30s; nếu không nhận `PONG` sau 10s → coi như mất kết nối, dọn dẹp khỏi Connection Manager, báo `AGENT_DISCONNECTED` cho Browser liên quan.
- **Reconnect**: Cả Agent và Web Client tự cài vòng lặp retry (exponential backoff: 1s, 2s, 4s... tối đa 30s) khi phát hiện WebSocket đóng ngoài ý muốn.
- **Message loop**: mỗi kết nối chạy 1 vòng lặp `while (webSocket.State == WebSocketState.Open) { await webSocket.ReceiveAsync(...); }` trên 1 Task riêng.

Đây là phần **phức tạp nhất khi bỏ SignalR** — nhóm nên viết kỹ và test kỹ phần Connection Manager trước, vì mọi chức năng khác đều phụ thuộc vào nó hoạt động ổn định.

---

## 5. Authentication & Pairing Flow

### 5.1 Đăng nhập Operator (Web Client)
1. Operator đăng ký tài khoản (username/password) — password hash bằng ASP.NET Core Identity (PBKDF2).
2. Đăng nhập → Gateway trả về JWT (thời hạn ngắn, ví dụ 2 giờ).
3. Web Client dùng JWT làm token khi mở kết nối **WebSocket (raw)** tới Gateway (gửi JWT ngay trong message đầu tiên/handshake, vì raw WebSocket không có sẵn cơ chế truyền token như SignalR).

### 5.2 Agent khởi động & đăng ký
1. Khi Agent chạy trên máy Target, nó tự mở kết nối **WebSocket (raw)** tới Gateway bằng một `AgentSecretKey` cấu hình sẵn (định danh máy, không phải thông tin nhạy cảm của người dùng).
2. Gateway cấp một `AgentId` (hoặc Agent tự có ID cố định lưu local) và ghi nhận trạng thái "online".
3. Agent sinh một **mã PIN 6 số**, hiển thị qua thông báo hệ thống (native notification) hoặc một cửa sổ nhỏ luôn-hiện-trên-cùng trên màn hình máy Target — Windows dùng 1 form nhỏ, macOS dùng `osascript -e 'display dialog'`/notification. PIN **đổi mới sau mỗi 5 phút** nếu chưa có ai pairing, hoặc dùng 1 lần rồi đổi ngay sau khi pairing thành công.

### 5.3 Ghép cặp (Pairing)
1. Operator trên Web Client nhập `AgentId` (hoặc chọn từ danh sách Agent online cùng LAN) + **PIN** hiển thị trên máy Target.
2. Gateway xác thực PIN khớp với Agent tương ứng → tạo `SessionId` duy nhất cho cặp Operator–Agent này.
3. Từ giờ, mọi message giữa 2 bên đều gắn `SessionId`; Gateway **chặn** mọi lệnh không đúng session đang active.
4. Agent hiển thị thông báo hệ thống định kỳ: *"Đang được điều khiển bởi [username]"* trong suốt thời gian session còn hiệu lực (không dùng tray thường trực — xem mục 4.1).

> Cơ chế PIN đóng vai trò xác thực lớp 2 thay cho OTP: PIN chỉ hiển thị **trực tiếp trên màn hình máy Target**, nên Operator bắt buộc phải có ai đó đứng trước máy Target đọc/nhập PIN → đảm bảo có sự đồng thuận vật lý trước khi cho phép điều khiển.

---

## 6. Chi tiết các chức năng bắt buộc

Tất cả lệnh đều đóng gói theo **Command Pattern** (xem mục 8) và truyền qua đúng 1 kênh WebSocket Gateway.

### 6.1 List App/Process
- Agent liệt kê tiến trình đang chạy bằng `System.Diagnostics.Process.GetProcesses()`.
- Trả về: PID, tên tiến trình, đường dẫn file thực thi, % CPU (nếu khả thi), dung lượng RAM đang dùng.
- Web Client hiển thị dạng bảng, có thể sort/filter theo tên.

### 6.2 Start/Stop (App/Process)
- **Start** (qua `IAppLauncherService` — mục 4.1):
  - Windows: Operator chọn file `.exe` (từ File Browser — mục 6.7) hoặc chọn từ danh sách ứng dụng đã cài (đọc từ Registry `Uninstall` key) → Agent `Process.Start(path)`.
  - macOS: Operator chọn ứng dụng `.app` trong `/Applications` → Agent chạy `Process.Start("open", $"-a \"{appPath}\"")`.
- **Stop**: Operator chọn 1 tiến trình từ List Process → Agent `Process.GetProcessById(pid).Kill()` (hoạt động giống nhau trên cả 2 OS vì `System.Diagnostics.Process` đa nền tảng).
- Ràng buộc: **không cho phép Stop các tiến trình hệ thống lõi**:
  - Windows: `System`, `svchost`, `csrss`, `wininit`, `services`, `lsass`, chính process của Agent.
  - macOS: `kernel_task`, `launchd`, `WindowServer`, `loginwindow`, chính process của Agent.
  - Danh sách chặn nên để trong file config theo OS, không hardcode, để dễ bổ sung.

### 6.3 Keylogger (có cơ chế xin phép)
- Agent cài hook bàn phím toàn hệ thống qua `IKeyboardHookService` (mục 4.1):
  - Windows: `SetWindowsHookEx(WH_KEYBOARD_LL, ...)`.
  - macOS: `CGEventTapCreate` — **yêu cầu người dùng cấp quyền Accessibility** cho Agent trong System Preferences → Security & Privacy → Accessibility trước khi hook hoạt động được (giới hạn cứng của macOS, không thể bỏ qua bằng code). Agent nên tự kiểm tra quyền này và hiển thị hướng dẫn nếu chưa được cấp.
- Ghi lại phím gõ kèm timestamp + tên cửa sổ đang active (để biết gõ ở đâu).
- **Bắt buộc luồng xin phép:**
  1. Khi Operator bấm "Bật Keylogger" trên Web Client → lệnh gửi tới Agent.
  2. Agent **hiển thị popup ngay trên màn hình máy Target** qua `INotificationService`: *"[username] muốn bật ghi log bàn phím trên máy này. Cho phép? [Đồng ý] [Từ chối]"* — Windows dùng dialog WinForms, macOS dùng `osascript -e 'display dialog ...'`.
  3. Chỉ khi người dùng máy Target bấm **Đồng ý**, Agent mới bắt đầu ghi log và gửi dữ liệu về Gateway/Web Client.
  4. Trong suốt thời gian Keylogger hoạt động, Agent gửi thông báo hệ thống định kỳ để cảnh báo liên tục — **không được phép chạy âm thầm không dấu vết**.
  5. Log được gửi theo batch (ví dụ mỗi 2 giây hoặc mỗi 50 ký tự) qua WebSocket, không gửi từng phím một để giảm tải mạng.
- Dữ liệu log lưu tạm ở Web Client trong phiên làm việc; không bắt buộc lưu vĩnh viễn vào DB (tùy nhóm quyết định thêm, nhưng nếu lưu phải mã hóa).

### 6.4 Webcam
- Dùng **OpenCvSharp4** để mở camera mặc định trên máy Target, chụp frame định kỳ (ví dụ mỗi 300ms), nén JPEG chất lượng vừa phải (giảm băng thông), gửi qua WebSocket (đã Base64-hóa trong JSON — xem mục 7).
- macOS sẽ tự hiện popup xin quyền truy cập Camera lần đầu (cơ chế bắt buộc của hệ điều hành, Agent không can thiệp được) — team nên xử lý và thông báo rõ nếu quyền bị từ chối.
- Web Client hiển thị như một luồng ảnh liên tục (giống video chậm — chấp nhận được trong LAN).
- Phải có thông báo khi webcam đang được truy cập (macOS có sẵn icon chấm cam/xanh khi camera đang mở; Windows nên tự gửi notification tương tự qua `INotificationService`).

### 6.5 Shutdown/Reset
- Lệnh nguy hiểm nhất trong hệ thống → **bắt buộc xác nhận 2 bước**:
  1. Operator bấm nút Shutdown/Restart trên Web Client.
  2. Web Client yêu cầu Operator **nhập lại mật khẩu đăng nhập** để xác nhận trước khi gửi lệnh xuống Gateway.
- Agent thực thi qua `IShutdownService`:
  - Windows: `Process.Start("shutdown", "/s /t 10")` (shutdown, delay 10s để có thể hủy khẩn cấp) hoặc `/r /t 10` cho restart.
  - macOS: `Process.Start("osascript", "-e 'tell app \"System Events\" to shut down'")` (kích hoạt hộp thoại tắt máy chuẩn của macOS) hoặc `restart` cho khởi động lại. Không dùng `sudo shutdown` trực tiếp vì cần mật khẩu hệ thống, phức tạp cho phạm vi đồ án.
- Log hành động này vào Audit Log với mức độ ưu tiên cao.

### 6.6 Xem màn hình (Screen View)
- Agent chụp màn hình định kỳ qua `IScreenCaptureService`:
  - Windows: `Graphics.CopyFromScreen`.
  - macOS: gọi lệnh `screencapture -x -t jpg <tempfile>` rồi đọc file vừa tạo (yêu cầu quyền **Screen Recording** trong System Preferences → Security & Privacy — bắt buộc từ macOS 10.15 trở lên; Agent nên phát hiện và hướng dẫn cấp quyền nếu thiếu).
- Resize xuống độ phân giải hợp lý (ví dụ tối đa 1280×720), nén JPEG (quality ~50-70%), Base64-hóa và gửi qua WebSocket (xem mục 7).
- Tần suất mặc định: **300–500ms/frame** (có thể cho Operator chỉnh chất lượng/tốc độ qua UI — xem Strategy Pattern mục 8).
- Đây là kênh chiếm băng thông nhiều nhất → cần theo dõi hiệu năng khi nhiều Agent stream cùng lúc trong LAN.

### 6.7 Copy Files (File Browser + Transfer)
- Agent expose API duyệt thư mục: liệt kê file/folder tại 1 đường dẫn, cho phép Operator điều hướng.
- **Danh sách thư mục/đường dẫn bị chặn tuyệt đối** (không được liệt kê, đọc, ghi, xóa):
  - **Windows**: `C:\Windows`, `C:\Windows\System32`, `C:\Program Files`, `C:\Program Files (x86)`, `C:\ProgramData`
  - **macOS**: `/System`, `/Library`, `/usr`, `/bin`, `/sbin`, `/private`, `~/Library` (thư mục cấu hình ẩn của user)
  - Thư mục cài đặt của chính Agent (cả 2 OS)
  - Bất kỳ đường dẫn nào Admin cấu hình thêm vào blacklist (file config trên Agent, tách riêng danh sách theo OS)
- Chống **path traversal**: mọi đường dẫn Operator gửi lên phải được `Path.GetFullPath()` chuẩn hóa và kiểm tra không nằm trong/không phải chuỗi con của các đường dẫn bị chặn trước khi xử lý.
- Truyền file: chia nhỏ thành chunk (ví dụ 64KB/chunk), Base64-hóa từng chunk, gửi tuần tự qua WebSocket kèm index + checksum (SHA-256 của toàn file) để bên nhận ráp lại và xác minh toàn vẹn dữ liệu sau khi nhận đủ.
- **Hỗ trợ đầy đủ cả hai chiều** (theo yêu cầu):
  - **Download**: Target → Operator (Operator tải file từ máy bị điều khiển về).
  - **Upload**: Operator → Target (Operator gửi file từ máy mình lên máy bị điều khiển) — áp dụng cùng path guard, không cho ghi vào thư mục blacklist.

---

## 7. Giao thức giao tiếp (Message Protocol)

Vì dùng **raw WebSocket**, để tránh phải đồng bộ 2 frame (text + binary) riêng biệt — dễ gây lỗi khi tự code — **mọi message, kể cả dữ liệu nhị phân (ảnh, file), đều đóng gói chung trong 1 JSON text frame duy nhất**, dữ liệu nhị phân được **Base64-hóa** trong `payload`. Đánh đổi ~33% dung lượng để lấy sự đơn giản/ổn định — chấp nhận được trong LAN.

```json
{
  "type": "COMMAND | RESPONSE | STREAM_FRAME | FILE_CHUNK | EVENT | PING | PONG",
  "action": "REGISTER_AGENT | REQUEST_PAIRING | PAIRING_RESULT | GET_PROCESS_LIST | START_PROCESS | STOP_PROCESS | SHUTDOWN | RESTART | ENABLE_KEYLOGGER | KEYLOGGER_CONSENT_REQUEST | KEYLOGGER_CONSENT_RESULT | SCREEN_FRAME | WEBCAM_FRAME | LIST_DIR | DOWNLOAD_FILE | UPLOAD_FILE_CHUNK | AGENT_DISCONNECTED | ...",
  "sessionId": "guid-của-phiên-pairing",
  "agentId": "guid-của-agent",
  "connectionId": "guid-của-kết-nối-websocket-hiện-tại",
  "timestamp": "ISO8601",
  "payload": {
    "dataBase64": "..."   // chỉ có khi action mang dữ liệu nhị phân (ảnh, file chunk)
  }
}
```

Danh sách `action` tối thiểu cần định nghĩa và chiều truyền:

| Action | Chiều | Mô tả |
|---|---|---|
| `REGISTER_AGENT` | Agent → Gateway | Agent báo online, gửi AgentId + AgentSecretKey + Platform (Windows/macOS) |
| `REQUEST_PAIRING` | Browser → Gateway | Gửi AgentId + PIN nhập vào |
| `PAIRING_RESULT` | Gateway → Browser | Thành công/thất bại + SessionId |
| *(các action điều khiển ở mục 6)* | Browser → Gateway → Agent | Gói lệnh, Gateway chỉ forward theo `sessionId`/`agentId`, không xử lý logic |
| *(action tương ứng trả kết quả)* | Agent → Gateway → Browser | Kết quả trả về (JSON, hoặc Base64 nếu có ảnh/file) |
| `SCREEN_FRAME` / `WEBCAM_FRAME` | Agent → Gateway → Browser | Frame màn hình/webcam (Base64 trong `payload.dataBase64`) |
| `KEYLOGGER_CONSENT_REQUEST` | Gateway → Agent | Kích hoạt popup xin phép trên Target |
| `KEYLOGGER_CONSENT_RESULT` | Agent → Gateway → Browser | Đồng ý/từ chối |
| `AGENT_DISCONNECTED` | Gateway → Browser | Báo mất kết nối Agent giữa chừng |
| `PING` / `PONG` | 2 chiều | Heartbeat giữ kết nối — xem mục 4.2 |

---

## 8. Design Patterns áp dụng

| Pattern | Áp dụng ở đâu | Lý do |
|---|---|---|
| **Command Pattern** | Mỗi hành động ở mục 6 (`GetProcessListCommand`, `StartProcessCommand`, `ShutdownCommand`...) implement chung interface `ICommand { Task<CommandResult> ExecuteAsync(CommandPayload payload); }` trên Agent | Thêm chức năng mới chỉ cần thêm 1 class Command mới, không sửa code cũ (Open/Closed Principle) |
| **Mediator Pattern** | Gateway (xử lý WebSocket thủ công, mục 4.2) | Gateway không biết logic nghiệp vụ của từng lệnh, chỉ định tuyến theo SessionId — giảm coupling giữa Browser và Agent |
| **Observer / Pub-Sub** | Luồng StreamFrame (màn hình, webcam, keylogger batch) | Agent "publish" dữ liệu định kỳ, Gateway forward tới đúng Browser đang theo dõi Session đó |
| **Strategy Pattern** | (1) Cấu hình chất lượng/tốc độ Screen Capture (Low/Medium/High); (2) mỗi implementation `IScreenCaptureService`, `IKeyboardHookService`... theo từng OS (mục 4.1) cũng là 1 Strategy có thể hoán đổi | Cho phép đổi thuật toán/OS-target mà không sửa code Agent core |
| **Factory Pattern** | `PlatformServiceFactory` trên Agent, dựa vào `RuntimeInformation.IsOSPlatform(...)` để khởi tạo đúng bộ implementation (Windows hoặc macOS) cho các interface ở mục 4.1 | Nơi duy nhất trong code biết đang chạy OS nào — phần còn lại của Agent hoàn toàn không cần biết |
| **Repository Pattern** | Tầng truy cập DB (`UserRepository`, `AuditLogRepository`, `AgentRepository`) trên Gateway | Tách biệt logic nghiệp vụ khỏi chi tiết EF Core, dễ test |
| **Singleton** | `ConnectionManager` trên Gateway quản lý danh sách Agent/Browser đang kết nối | Cần 1 nguồn dữ liệu trạng thái kết nối duy nhất, tránh xung đột |

---

## 9. Bảo mật (Security Checklist)

- [ ] Mật khẩu hash bằng ASP.NET Core Identity, không lưu plaintext.
- [ ] JWT có thời hạn ngắn (2h), refresh token nếu cần dùng lâu.
- [ ] PIN pairing: 6 số, hết hạn sau 5 phút hoặc dùng 1 lần.
- [ ] Dùng `ws://` (không mã hóa TLS) theo quyết định của nhóm để đơn giản hóa triển khai — **chỉ chấp nhận được vì hệ thống chạy trong LAN kín, khép kín cho mục đích demo đồ án**. ⚠️ Nên ghi rõ trong báo cáo: đây là điểm đánh đổi bảo mật có chủ đích (traffic có thể bị nghe lén bởi thiết bị khác cùng LAN), không phù hợp nếu triển khai thật ngoài phạm vi lớp học — nếu mở rộng sau này nên nâng cấp lên `wss://`.
- [ ] Rate-limit số lần đăng nhập sai (chống brute-force) — ví dụ khóa 5 phút sau 5 lần sai.
- [ ] Kiểm tra `SessionId` hợp lệ trên **mọi** message trước khi Gateway forward — không tin tưởng client.
- [ ] Blacklist đường dẫn hệ thống cho Copy Files (mục 6.7), chống path traversal.
- [ ] Blacklist tiến trình hệ thống cho Stop Process (mục 6.2).
- [ ] Shutdown/Restart bắt buộc xác nhận lại mật khẩu (mục 6.5).
- [ ] Keylogger bắt buộc popup xin phép tại Target, hiển thị trạng thái liên tục khi đang hoạt động (mục 6.3).
- [ ] Audit Log: ghi lại {ai, làm gì, trên Agent nào, lúc nào} cho **mọi lệnh** — đặc biệt Shutdown, Stop Process, File Delete, Keylogger toggle.

---

## 10. Database Schema (đề xuất, SQLite + EF Core)

```
Users
  Id (PK), Username, PasswordHash, CreatedAt

Agents
  Id (PK), AgentName, Platform (Windows | MacOS), AgentSecretKeyHash, LastSeenIP, LastOnlineAt

Sessions (pairing sessions)
  Id (PK), UserId (FK), AgentId (FK), StartedAt, EndedAt, Status

AuditLogs
  Id (PK), SessionId (FK), UserId (FK), AgentId (FK), Action, Payload (JSON), Timestamp, Result
```

---

## 11. Cấu trúc thư mục dự án (Monorepo)

```
/RemoteControlLAN
├── README.md                     ← file này
├── /src
│   ├── /Gateway                  (ASP.NET Core 8 project)
│   │   ├── /WebSockets            (xử lý WebSocket thủ công: ConnectionManager.cs, MessageRouter.cs)
│   │   ├── /Commands             (định nghĩa Command DTO dùng chung)
│   │   ├── /Services             (PairingService, ConnectionManager, AuditService)
│   │   ├── /Data                 (AppDbContext, Migrations)
│   │   ├── /Models                (Entity: User, Agent, Session, AuditLog)
│   │   └── Program.cs
│   ├── /Agent                    (.NET 8 project — Console/Background Service, không cửa sổ chính — xem mục 4.1)
│   │   ├── /Commands              (mỗi Command 1 file: StartProcessCommand.cs, ...)
│   │   ├── /Capture                (ScreenCapture.cs, WebcamCapture.cs)
│   │   ├── /Hooks                  (KeyboardHook.cs)
│   │   ├── /Security                (PathGuard.cs — kiểm tra blacklist path)
│   │   ├── /Services                (GatewayConnection.cs — wrapper raw WebSocket client, dùng `ClientWebSocket`)
│   │   └── Program.cs
│   └── /WebClient                 (React + TS + Vite)
│       ├── /src
│       │   ├── /components         (ScreenViewer, FileBrowser, ProcessList, ...)
│       │   ├── /pages               (Login, Dashboard, AgentControl)
│       │   ├── /hooks                (useWebSocket.ts)
│       │   └── /services              (api.ts, wsClient.ts)
│       └── package.json
└── /docs
    └── kien-truc-chi-tiet.md      (mở rộng thêm nếu cần, không sửa README gốc)
```

---

## 12. Coding Conventions

**C# (Gateway + Agent):**
- PascalCase cho class, method, property.
- camelCase cho biến local, tham số.
- Field private: `_camelCase`.
- Method bất đồng bộ luôn có hậu tố `Async` (`GetProcessListAsync`).
- Mỗi Command là 1 class riêng, implement `ICommand`.
- Dùng `async/await` xuyên suốt, không block bằng `.Result` hoặc `.Wait()`.

**TypeScript / React (Web Client):**
- Component: PascalCase, function component + Hooks (không dùng class component).
- Hàm/biến: camelCase.
- 1 component = 1 file, đặt tên file trùng tên component.
- Toàn bộ gọi WebSocket đi qua 1 service tập trung (`wsClient.ts`), không gọi rải rác trong component.

**Git:**
- Nhánh: `feature/ten-chuc-nang`, `fix/mo-ta-loi`.
- Commit theo Conventional Commits: `feat:`, `fix:`, `docs:`, `refactor:`, `chore:`.
- Mỗi chức năng ở mục 6 nên là 1 nhánh riêng, PR review giữa 3 thành viên trước khi merge vào `main`.

---

## 13. Yêu cầu phi chức năng (Non-functional)

- Chạy ổn định trong mạng LAN (không cần tối ưu cho Internet/NAT traversal).
- Screen streaming: mặc định 300–500ms/frame, cho phép chỉnh trong UI.
- File transfer: chunk 64KB, có progress bar và checksum verify.
- Gateway phải xử lý được ít nhất 5–10 session song song (đủ cho demo lớp học) mà không giật/lag đáng kể.
- Reconnect tự động khi mất kết nối tạm thời (tự cài đặt exponential backoff — xem mục 4.2, vì raw WebSocket không có sẵn cơ chế này như SignalR), Agent/Browser phải báo rõ trạng thái "Đang kết nối lại..." trên UI.

---

## 14. Hướng dẫn chạy môi trường Dev

```bash
# Gateway
cd src/Gateway
dotnet ef database update   # tạo DB SQLite từ migrations
dotnet run                  # mặc định chạy tại https://localhost:5001

# Agent
cd src/Agent
dotnet run                  # chạy trên máy Target, cấu hình GatewayUrl trong appsettings.json

# Web Client
cd src/WebClient
npm install
npm run dev                 # mặc định chạy tại http://localhost:5173
```

---

## 15. Đề xuất chia việc cho nhóm 3 người

| Thành viên | Phụ trách |
|---|---|
| A | Gateway (Auth, WebSocket Connection Manager, Pairing, Audit Log, DB) |
| B | Agent (Capture màn hình/webcam, Keylogger, Process control, File Guard) |
| C | Web Client (UI toàn bộ, tích hợp WebSocket client, UX luồng pairing) |

Đề xuất mốc:
1. **Tuần 1–2**: Auth + Pairing hoạt động end-to-end (chưa có chức năng thật, chỉ là "bắt tay" được).
2. **Tuần 3–4**: List Process, Start/Stop, Shutdown (các lệnh JSON đơn giản, không streaming).
3. **Tuần 5–6**: Screen View + Webcam (phần streaming, khó nhất).
4. **Tuần 7**: Copy Files (file transfer + path guard).
5. **Tuần 8**: Keylogger (kèm cơ chế consent) — để cuối vì nhạy cảm nhất, cần code chắc tay.
6. **Tuần cuối**: Audit log hoàn chỉnh, polish UI, viết báo cáo, chuẩn bị demo.

---

## 16. Các điểm đã được xác nhận

1. **Raw WebSocket** (mục 4) — nhóm xác nhận dùng **raw WebSocket** thuần túy, không dùng SignalR. Toàn bộ tài liệu (mục 5, 11, 12, 13, 15) đã được cập nhật đồng nhất theo lựa chọn này.
2. **Shutdown cần xác nhận lại mật khẩu** (mục 6.5) — xác nhận giữ nguyên bước xác thực 2 lớp (Operator phải nhập lại mật khẩu đăng nhập) trước khi Gateway forward lệnh Shutdown/Restart xuống Agent.
3. **Danh sách blacklist thư mục/tiến trình** (mục 6.2, 6.7) — xác nhận giữ nguyên danh sách đã liệt kê trong tài liệu, cứ làm theo những gì đã ghi, không cần rà soát/mở rộng thêm trước khi code.
4. **Copy Files hỗ trợ cả 2 chiều** (mục 6.7) — xác nhận triển khai đầy đủ cả Download (Target → Operator) và Upload (Operator → Target).
5. **Dùng `ws://` (không TLS)** (mục 9) — xác nhận dùng `ws://` thuần để đơn giản hóa triển khai, chấp nhận đánh đổi bảo mật đã ghi rõ ở mục 9 (chỉ phù hợp trong LAN kín của đồ án).

Các điểm trên đã chốt — có thể tiến hành viết tài liệu chi tiết hơn cho từng module (đặc tả đầy đủ các `action` trong Message Protocol ở mục 7, hoặc bắt đầu scaffold code mẫu) khi cần.

---

## 17. Làm rõ khi triển khai

- Agent gửi action nội bộ `UPDATE_PAIRING_PIN` ngay sau `REGISTER_AGENT_RESULT`. Payload là `{ "pin": "123456" }`. Gateway chỉ nhận action này từ WebSocket Agent đã xác thực, hash PIN rồi đặt hạn 5 phút. Bổ sung này hoàn thiện bước Agent tự sinh PIN ở mục 5.2 mà không đưa PIN vào REST hay cấu hình tĩnh.
- Lệnh Shutdown/Restart mặc định bị chặn bởi `Agent:AllowPowerCommands=false`. Chỉ bật thành `true` khi demo trên thiết bị đã được phép; Gateway vẫn bắt buộc re-verify password trước khi forward.
- Bản Agent hiện cô lập global keyboard hook sau `IKeyboardHookService` và chỉ khởi động nó sau dialog consent. Mỗi nền tảng cần kiểm thử/đóng gói hook native riêng với quyền Accessibility (macOS) trước khi dùng ngoài demo, không được thay bằng cơ chế chạy ẩn.
