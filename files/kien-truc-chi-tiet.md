# Kiến Trúc Chi Tiết — Đặc Tả Message Protocol & Module

> Tài liệu mở rộng của `README.md` (theo đúng quy ước ở mục 11 — không sửa README gốc). Mục tiêu: đặc tả đầy đủ từng `action` trong Message Protocol (README mục 7) để 3 thành viên code khớp payload với nhau ngay từ đầu, tránh việc mỗi người tự đoán field.
>
> Quy ước đọc: mọi field đánh dấu **(bắt buộc)** phải luôn có mặt; field còn lại là optional. Mọi message vẫn theo đúng khung chung ở README mục 7 (`type`, `action`, `sessionId`, `agentId`, `connectionId`, `timestamp`, `payload`) — tài liệu này chỉ mô tả chi tiết bên trong `payload` của từng `action`.

---

## 1. Quy ước chung

- Mọi `payload` là 1 object JSON phẳng (không lồng quá 2 cấp) để dễ deserialize sang C# record/DTO.
- Trường thời gian dùng ISO8601 UTC (`2026-07-30T10:15:00Z`).
- Trường ID (transferId, sessionId, agentId, connectionId) đều là GUID dạng string.
- Nếu 1 action có thể thất bại, Gateway/Agent trả về action `ERROR` riêng (xem mục 5) thay vì nhồi field `success` vào mọi payload — ngoại trừ các action có kết quả 2 trạng thái đơn giản (thành công/thất bại kèm lý do), các action đó vẫn giữ field `success` cho gọn.
- Toàn bộ action dưới đây map 1-1 với 1 class `ICommand` (xem mục 4).

---

## 2. Đặc tả từng Action

### 2.1 Đăng ký & Pairing

#### `REGISTER_AGENT`
- **Chiều**: Agent → Gateway
- **type**: `EVENT`
- **Khi nào gửi**: Ngay khi Agent khởi động và mở WebSocket tới Gateway thành công.
- **payload**:
  ```json
  {
    "agentSecretKey": "string (bắt buộc)",
    "platform": "Windows | MacOS (bắt buộc)",
    "hostname": "string (bắt buộc)",
    "agentVersion": "string, vd 1.0.0"
  }
  ```
- **Phản hồi**: Gateway trả `REGISTER_AGENT_RESULT` (`type: RESPONSE`):
  ```json
  { "success": true, "agentId": "guid", "message": "string" }
  ```
  Nếu `agentSecretKey` sai → `success: false`, Gateway đóng kết nối sau khi gửi.

#### `REQUEST_PAIRING`
- **Chiều**: Browser → Gateway
- **type**: `COMMAND`
- **payload**:
  ```json
  { "agentId": "guid (bắt buộc)", "pin": "string 6 số (bắt buộc)" }
  ```

#### `PAIRING_RESULT`
- **Chiều**: Gateway → Browser (và Gateway → Agent để Agent biết dừng hiển thị PIN)
- **type**: `RESPONSE`
- **payload**:
  ```json
  {
    "success": true,
    "sessionId": "guid (chỉ có khi success=true)",
    "message": "string"
  }
  ```
- **Lỗi thường gặp**: PIN sai, PIN hết hạn, Agent đã có session khác đang active (mặc định: 1 Agent chỉ nhận 1 session tại 1 thời điểm — nếu nhóm muốn cho phép nhiều Operator xem cùng lúc thì cần bàn thêm, hiện tại đặc tả theo 1-1).

---

### 2.2 Process Management

#### `GET_PROCESS_LIST`
- **Chiều**: Browser → Gateway → Agent
- **type**: `COMMAND`
- **payload**: `{}` (rỗng)

#### `PROCESS_LIST_RESULT`
- **Chiều**: Agent → Gateway → Browser
- **type**: `RESPONSE`
- **payload**:
  ```json
  {
    "processes": [
      { "pid": 1234, "name": "chrome", "path": "C:\\...\\chrome.exe", "cpuPercent": 12.5, "memoryMB": 340 }
    ]
  }
  ```
  Nếu không lấy được `cpuPercent` (macOS hạn chế quyền) → để `null`, Web Client hiển thị "—".

#### `START_PROCESS`
- **Chiều**: Browser → Gateway → Agent
- **payload**:
  ```json
  { "targetPath": "string (bắt buộc) — đường dẫn .exe (Windows) hoặc .app (macOS)" }
  ```

#### `START_PROCESS_RESULT`
- **payload**: `{ "success": true, "pid": 5678, "message": "string" }`

#### `STOP_PROCESS`
- **payload**: `{ "pid": 1234 (bắt buộc) }`

#### `STOP_PROCESS_RESULT`
- **payload**: `{ "success": true, "message": "string" }`
- **Lỗi**: nếu `pid` nằm trong blacklist tiến trình hệ thống (README mục 6.2) → `ERROR` với `code: PROCESS_PROTECTED`.

---

### 2.3 Shutdown / Restart (luồng xác nhận 2 bước)

Vì đây là lệnh nguy hiểm nhất, việc xác nhận mật khẩu **không đi qua WebSocket** mà qua REST endpoint đã có sẵn của Auth (tái dùng, không cần thêm hạ tầng):

1. Operator bấm Shutdown trên UI → Web Client gọi `POST /api/auth/reverify-password` (REST, có JWT header) với body `{ "password": "..." }`.
2. Nếu đúng, Gateway trả về `confirmationToken` (JWT ngắn hạn, hạn dùng 60 giây, chỉ dùng được 1 lần, có claim `sessionId` gắn kèm để không dùng chéo session).
3. Web Client gửi action `SHUTDOWN`/`RESTART` qua WebSocket kèm `confirmationToken` này.
4. Gateway xác thực token (đúng session, chưa hết hạn, chưa dùng) trước khi forward xuống Agent.

#### `SHUTDOWN` / `RESTART`
- **Chiều**: Browser → Gateway → Agent
- **payload**:
  ```json
  {
    "confirmationToken": "string (bắt buộc)",
    "delaySeconds": 10
  }
  ```
- **Lỗi**: `confirmationToken` sai/hết hạn/đã dùng → `ERROR` với `code: CONFIRMATION_INVALID`, Gateway **không** forward xuống Agent trong trường hợp này (chặn ngay tại Gateway).

#### `SHUTDOWN_RESULT` / `RESTART_RESULT`
- **payload**: `{ "success": true, "message": "string" }`
- Đồng thời Gateway ghi Audit Log mức ưu tiên cao (README mục 9) bất kể thành công hay thất bại.

---

### 2.4 Keylogger (có consent)

Đơn giản hoá so với bảng action gốc ở README mục 7: không cần action `KEYLOGGER_CONSENT_REQUEST` riêng, vì `ENABLE_KEYLOGGER` khi tới Agent sẽ **tự động kích hoạt popup xin phép** trên máy Target (không cần Gateway gửi thêm 1 message khác) — giảm 1 round-trip không cần thiết.

#### `ENABLE_KEYLOGGER`
- **Chiều**: Browser → Gateway → Agent
- **payload**: `{}`
- **Hành vi phía Agent**: nhận action này → hiển thị popup xin phép (README mục 6.3) → **không** bật hook ngay, chờ kết quả popup.

#### `KEYLOGGER_CONSENT_RESULT`
- **Chiều**: Agent → Gateway → Browser
- **type**: `EVENT`
- **payload**: `{ "accepted": true, "respondedAt": "ISO8601" }`
- Nếu `accepted: false` → Agent không bật hook, Web Client hiển thị "Người dùng từ chối".

#### `KEYLOG_BATCH`
- **Chiều**: Agent → Gateway → Browser (chỉ gửi khi đã được đồng ý)
- **type**: `STREAM_FRAME`
- **payload**:
  ```json
  {
    "entries": [
      { "text": "hello", "windowTitle": "Notepad", "timestamp": "ISO8601" }
    ]
  }
  ```
- Gửi theo batch mỗi ~2 giây hoặc 50 ký tự (README mục 6.3), không gửi từng phím.

#### `DISABLE_KEYLOGGER`
- **Chiều**: Browser → Gateway → Agent
- **payload**: `{}`
- **Phản hồi**: `DISABLE_KEYLOGGER_RESULT` — `{ "success": true }`. Agent dừng hook và ngừng gửi thông báo trạng thái liên tục.

---

### 2.5 Screen View & Webcam

#### `START_SCREEN_VIEW` / `STOP_SCREEN_VIEW`
- **Chiều**: Browser → Gateway → Agent
- **payload (START)**:
  ```json
  { "quality": "low | medium | high", "intervalMs": 400 }
  ```
  `intervalMs` optional — nếu bỏ trống, Agent dùng mặc định theo `quality` (vd low=800ms, medium=400ms, high=200ms).

#### `SCREEN_FRAME`
- **Chiều**: Agent → Gateway → Browser
- **type**: `STREAM_FRAME`
- **payload**:
  ```json
  {
    "dataBase64": "string (bắt buộc, JPEG đã nén)",
    "width": 1280,
    "height": 720,
    "frameIndex": 42,
    "capturedAt": "ISO8601"
  }
  ```

#### `START_WEBCAM` / `STOP_WEBCAM` / `WEBCAM_FRAME`
- Cấu trúc payload tương tự `SCREEN_FRAME` (không có `quality` tuỳ chỉnh vì webcam không cần nhiều mức nén).
- **Lỗi riêng của Webcam**: nếu macOS từ chối quyền Camera → Agent gửi `ERROR` với `code: PERMISSION_DENIED`, kèm `message` hướng dẫn vào System Preferences → Security & Privacy → Camera.

---

### 2.6 File Browser & Transfer

#### `LIST_DIR`
- **payload**: `{ "path": "string (bắt buộc)" }`

#### `LIST_DIR_RESULT`
- **payload**:
  ```json
  {
    "path": "C:\\Users\\...",
    "entries": [
      { "name": "report.docx", "isDirectory": false, "sizeBytes": 20480, "modifiedAt": "ISO8601" }
    ]
  }
  ```
- **Lỗi**: path nằm trong blacklist hoặc path traversal bị phát hiện → `ERROR` với `code: PATH_BLOCKED` (không tiết lộ path thật sự bị chặn ở đâu, chỉ báo "đường dẫn không hợp lệ").

#### `DOWNLOAD_FILE` (Target → Operator)
- **Chiều**: Browser → Gateway → Agent
- **payload**: `{ "path": "string (bắt buộc)", "transferId": "guid do Web Client tự sinh" }`
- **Agent phản hồi bằng chuỗi**:
  1. Nhiều `FILE_CHUNK` liên tiếp (`type: FILE_CHUNK`):
     ```json
     { "transferId": "guid", "chunkIndex": 0, "totalChunks": 50, "dataBase64": "..." }
     ```
  2. Kết thúc bằng `FILE_TRANSFER_COMPLETE`:
     ```json
     { "transferId": "guid", "success": true, "sha256": "hex-string", "message": "string" }
     ```
  Web Client ráp các chunk theo `chunkIndex`, verify bằng `sha256` sau khi nhận đủ `totalChunks`.

#### `UPLOAD_FILE_INIT` (Operator → Target, bước 1)
- **Chiều**: Browser → Gateway → Agent
- **payload**:
  ```json
  {
    "transferId": "guid",
    "targetPath": "string (bắt buộc) — thư mục đích trên Target",
    "fileName": "string (bắt buộc)",
    "totalChunks": 50,
    "sha256": "hex-string — checksum của file gốc"
  }
  ```
- **Phản hồi**: `UPLOAD_FILE_INIT_RESULT` — `{ "transferId": "guid", "accepted": true, "message": "string" }`. Agent kiểm tra `targetPath` có nằm trong blacklist không **ngay ở bước này**, từ chối sớm nếu vi phạm (tránh nhận hết file rồi mới báo lỗi).

#### `UPLOAD_FILE_CHUNK` (bước 2, lặp lại)
- **Chiều**: Browser → Gateway → Agent
- **payload**: `{ "transferId": "guid", "chunkIndex": 0, "dataBase64": "..." }`

#### `UPLOAD_FILE_RESULT` (bước 3, sau chunk cuối)
- **Chiều**: Agent → Gateway → Browser
- **payload**: `{ "transferId": "guid", "success": true, "message": "string" }`
- Agent tự verify `sha256` sau khi ráp đủ chunk trước khi ghi file cuối cùng vào đĩa (ghi ra file tạm `.part` trước, rename sau khi verify thành công — tránh file half-written nếu transfer bị đứt giữa chừng).

---

### 2.7 Connection lifecycle

#### `AGENT_DISCONNECTED`
- **Chiều**: Gateway → Browser
- **payload**: `{ "agentId": "guid", "lastSeenAt": "ISO8601" }`
- Gửi khi Heartbeat (README mục 4.2) phát hiện mất PONG sau 10s.

#### `AGENT_RECONNECTED`
- **Chiều**: Gateway → Browser
- **payload**: `{ "agentId": "guid", "sessionId": "guid" }`
- Gửi khi Agent reconnect và Gateway khôi phục lại đúng session cũ (nếu còn hạn) mà không cần pairing lại bằng PIN.

#### `PING` / `PONG`
- **payload**: `{}` — không mang dữ liệu, chỉ dùng để giữ kết nối.

---

## 3. Mã lỗi chung (action `ERROR`)

Khi 1 lệnh thất bại vì lý do hệ thống (không phải kết quả nghiệp vụ bình thường như "PID không tồn tại"), bên gửi trả về:

```json
{
  "type": "RESPONSE",
  "action": "ERROR",
  "payload": {
    "code": "PATH_BLOCKED",
    "message": "Đường dẫn không hợp lệ",
    "relatedAction": "LIST_DIR"
  }
}
```

| code | Ý nghĩa |
|---|---|
| `AUTH_FAILED` | JWT không hợp lệ/hết hạn |
| `SESSION_INVALID` | `sessionId` không khớp session đang active |
| `PIN_INVALID` | PIN nhập sai lúc pairing |
| `PIN_EXPIRED` | PIN đã hết hạn (quá 5 phút) |
| `CONFIRMATION_INVALID` | Token xác nhận mật khẩu cho Shutdown sai/hết hạn/đã dùng |
| `PERMISSION_DENIED` | Agent thiếu quyền OS (Accessibility, Screen Recording, Camera trên macOS) |
| `PATH_BLOCKED` | Path nằm trong blacklist hoặc path traversal |
| `PROCESS_PROTECTED` | PID nằm trong danh sách tiến trình hệ thống được bảo vệ |
| `RATE_LIMITED` | Quá số lần đăng nhập sai cho phép |
| `AGENT_OFFLINE` | Gateway không forward được vì Agent đã mất kết nối |
| `UNKNOWN_ACTION` | Action không tồn tại/không được hỗ trợ |

---

## 4. Mapping Action ↔ Command Pattern (README mục 8)

```csharp
public interface ICommand
{
    Task<CommandResult> ExecuteAsync(CommandPayload payload);
}

public class CommandResult
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public object Data { get; set; } // serialize thành payload tương ứng ở trên
}
```

Mỗi `action` ở mục 2 tương ứng 1 class implement `ICommand`, ví dụ:

| Action | Command class (Agent) |
|---|---|
| `GET_PROCESS_LIST` | `GetProcessListCommand` |
| `START_PROCESS` | `StartProcessCommand` |
| `STOP_PROCESS` | `StopProcessCommand` |
| `SHUTDOWN` / `RESTART` | `ShutdownCommand` / `RestartCommand` |
| `ENABLE_KEYLOGGER` / `DISABLE_KEYLOGGER` | `EnableKeyloggerCommand` / `DisableKeyloggerCommand` |
| `START_SCREEN_VIEW` / `STOP_SCREEN_VIEW` | `StartScreenViewCommand` / `StopScreenViewCommand` |
| `START_WEBCAM` / `STOP_WEBCAM` | `StartWebcamCommand` / `StopWebcamCommand` |
| `LIST_DIR` | `ListDirCommand` |
| `DOWNLOAD_FILE` | `DownloadFileCommand` |
| `UPLOAD_FILE_INIT` / `UPLOAD_FILE_CHUNK` | `UploadFileInitCommand` / `UploadFileChunkCommand` |

Gateway chỉ có 1 `MessageRouter` đọc field `action`, tra bảng để gọi đúng Command bên phía nhận (Agent hoặc xử lý nội bộ nếu là action thuộc Gateway như `REQUEST_PAIRING`) — không có `if/else` dài dòng, thêm action mới chỉ cần đăng ký thêm 1 dòng vào bảng tra cứu.

---

## 5. Luồng xử lý chính (sequence, dạng rút gọn)

### 5.1 Pairing
```
Agent --REGISTER_AGENT--> Gateway --REGISTER_AGENT_RESULT--> Agent
Agent hiển thị PIN trên màn hình Target
Browser --REQUEST_PAIRING(agentId, pin)--> Gateway
Gateway kiểm tra PIN --PAIRING_RESULT(sessionId)--> Browser
Gateway --PAIRING_RESULT--> Agent (để Agent ẩn PIN, bắt đầu hiện "đang được điều khiển bởi ...")
```

### 5.2 Shutdown (2 bước xác nhận)
```
Operator bấm Shutdown trên UI
Browser --POST /api/auth/reverify-password--> Gateway (REST, không qua WS)
Gateway --confirmationToken--> Browser
Browser --SHUTDOWN(confirmationToken)--> Gateway
Gateway xác thực token --> forward --> Agent
Agent thực thi --SHUTDOWN_RESULT--> Gateway --> Browser
Gateway ghi Audit Log (ưu tiên cao)
```

### 5.3 Keylogger consent
```
Browser --ENABLE_KEYLOGGER--> Gateway --> Agent
Agent hiện popup xin phép trên máy Target
Người dùng Target bấm Đồng ý/Từ chối
Agent --KEYLOGGER_CONSENT_RESULT(accepted)--> Gateway --> Browser
Nếu accepted=true: Agent bắt đầu gửi KEYLOG_BATCH định kỳ
```

### 5.4 Upload file (Operator → Target)
```
Browser --UPLOAD_FILE_INIT(targetPath, sha256, totalChunks)--> Gateway --> Agent
Agent kiểm tra blacklist path --UPLOAD_FILE_INIT_RESULT(accepted)--> Gateway --> Browser
Nếu accepted=true:
  Browser --UPLOAD_FILE_CHUNK x N--> Gateway --> Agent (Agent ghi vào file .part)
  Agent verify sha256 sau chunk cuối --> rename file .part thành file thật
  Agent --UPLOAD_FILE_RESULT--> Gateway --> Browser
```

---

## 6. Quy ước đặt tên field (đã chốt) & việc còn lại trước khi scaffold code

**Quy ước đặt tên field: camelCase** cho toàn bộ payload JSON — thống nhất với coding convention TypeScript ở README mục 12, áp dụng chung cho cả Gateway và Agent, không cần bàn thêm. Phía C# dùng 1 cấu hình `JsonSerializerOptions` dùng chung thay vì gắn `[JsonPropertyName]` thủ công lên từng property:

```csharp
// Đặt trong 1 shared class dùng chung cho Gateway và Agent khi serialize/deserialize message
public static class JsonConfig
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
```

Property C# vẫn viết PascalCase như bình thường (`AgentId`, `SessionId`, `TargetPath`...) — policy này tự động map sang camelCase khi ra JSON (`agentId`, `sessionId`, `targetPath`...), khớp đúng với mọi ví dụ payload ở mục 2. Cả Gateway và Agent phải dùng chung `JsonConfig.Default` này ở mọi nơi gọi `JsonSerializer.Serialize`/`Deserialize` để tránh lệch convention giữa 2 phía.

Scaffold code cho 2 phần này đã được viết sẵn (không còn là việc tồn đọng):

- [x] DTO C# cho từng payload ở mục 2 → `src/Shared/Messages/Payloads.cs`, dùng chung với `MessageEnvelope.cs` và `JsonConfig.cs` trong cùng thư mục (dùng chung được cho cả Gateway lẫn Agent, đúng như dự định).
- [x] `MessageRouter` (mục 4) → `src/Gateway/WebSockets/MessageRouter.cs`, đi kèm `ConnectionManager.cs` trong cùng thư mục (Router cần Connection Manager để biết forward message tới đúng WebSocket nào).

Việc thật sự còn lại — đây là phần logic nghiệp vụ cụ thể, không phải phần khung sườn, nên vẫn cần code tay:

- [ ] Cài đặt 2 interface `IAuthService` và `IPairingService` (đã định nghĩa sẵn trong `src/Gateway/Services/GatewayServiceInterfaces.cs`, `MessageRouter` đã gọi qua interface này) — thuộc phần việc của thành viên A theo README mục 15 (Auth, Pairing).
- [ ] Nối `ConnectionManager` + `MessageRouter` vào pipeline WebSocket thật của ASP.NET Core: middleware `app.UseWebSockets()` gọi `HttpContext.WebSockets.AcceptWebSocketAsync()`, sinh 1 `connectionId` mới, gọi `connections.AddConnection(connectionId, socket)`, rồi chạy vòng lặp `ReceiveAsync` gọi `router.RouteAsync(json, connectionId)` cho từng message nhận được (README mục 4.2 — "Message loop").
