# Kiến trúc chi tiết và Message Protocol

Tài liệu này mở rộng [README](../README.md) và phản ánh implementation trong `src/`.

## Thành phần và trách nhiệm

| Thành phần | Đường dẫn | Trách nhiệm |
|---|---|---|
| Shared | `src/Shared` | `MessageEnvelope`, JSON camelCase và DTO dùng chung |
| Gateway | `src/Gateway` | JWT, SQLite/EF Core, pairing, audit, WebSocket mediator và heartbeat |
| Agent | `src/Agent` | Kết nối lại, command dispatcher, platform services, path/process guard, streaming/transfer |
| Web Client | `src/WebClient` | React UI, REST auth và một WebSocket client tập trung |

## Envelope chuẩn

Mọi WebSocket message là một text JSON frame:

```json
{
  "type": "COMMAND | RESPONSE | STREAM_FRAME | FILE_CHUNK | EVENT | PING | PONG",
  "action": "GET_PROCESS_LIST",
  "sessionId": "guid-optional",
  "agentId": "guid-optional",
  "timestamp": "2026-07-30T00:00:00Z",
  "payload": {}
}
```

Gateway chỉ xử lý `REGISTER_AGENT`, `UPDATE_PAIRING_PIN`, `REQUEST_PAIRING`, `PING`/`PONG`; các action còn lại được xác thực session và chuyển tiếp nguyên vẹn.

## Pairing và xác thực

1. Web Client gọi `POST /api/auth/register` hoặc `POST /api/auth/login`; JWT có hạn 2 giờ.
2. Agent mở `ws://gateway/ws`, gửi `REGISTER_AGENT` cùng `agentId` và AgentSecretKey.
3. Sau khi Gateway trả `REGISTER_AGENT_RESULT`, Agent sinh PIN 6 số, hiển thị qua notification, rồi gửi `UPDATE_PAIRING_PIN`. Gateway chỉ lưu hash của PIN trong 5 phút.
4. Browser gửi `REQUEST_PAIRING(agentId, pin)`. Khi đúng, Gateway tạo `RemoteSession`, bind hai connection và trả `PAIRING_RESULT` cho cả hai bên.
5. Mọi command sau đó bắt buộc có `sessionId` hợp lệ, đúng connection và đúng chiều Browser→Agent hoặc Agent→Browser.

`SHUTDOWN`/`RESTART` yêu cầu thêm token dùng một lần: Browser gọi `POST /api/auth/reverify-password`, sau đó đưa token (hạn 60 giây) vào `ShutdownPayload`. Gateway tiêu thụ token trước khi relay.

## Action đã cài đặt

| Action | Hướng | Handler |
|---|---|---|
| `GET_PROCESS_LIST`, `START_PROCESS`, `STOP_PROCESS` | Browser → Agent | `AgentCommandDispatcher` |
| `START_SCREEN_VIEW`, `STOP_SCREEN_VIEW`, `SCREEN_FRAME` | Browser ↔ Agent | `IScreenCaptureService` + stream task |
| `START_WEBCAM`, `STOP_WEBCAM`, `WEBCAM_FRAME` | Browser ↔ Agent | OpenCvSharp4 |
| `LIST_DIR`, `DOWNLOAD_FILE`, `UPLOAD_FILE_INIT`, `UPLOAD_FILE_CHUNK` | Browser ↔ Agent | `PathGuard` + `FileTransferService` |
| `ENABLE_KEYLOGGER`, `DISABLE_KEYLOGGER` | Browser ↔ Agent | target-side consent qua `INotificationService` |
| `SHUTDOWN`, `RESTART` | Browser → Agent | Gateway reverify + `IShutdownService` |

File download chia 64KB/chunk và kết thúc bằng SHA-256. Upload phải được Agent chấp nhận `UPLOAD_FILE_INIT_RESULT` trước khi Browser gửi chunk; Agent ghi `.part`, xác minh SHA-256 rồi mới rename file cuối.

## Connection lifecycle

Gateway phát `PING` mỗi 30 giây. Kết nối không có hoạt động hơn 40 giây bị loại khỏi `ConnectionManager`; Browser trong session tương ứng nhận `AGENT_DISCONNECTED`. Agent và Browser đều có reconnect exponential backoff, tối đa 30 giây.

## Bảo vệ bắt buộc

- `PathGuard` chuẩn hóa bằng `Path.GetFullPath` và kiểm tra theo prefix thư mục, không kiểm tra bằng chuỗi con.
- `ProcessGuard` chặn tiến trình lõi hệ điều hành và chính Agent.
- Agent mặc định `AllowPowerCommands=false`.
- Keylogging chỉ bắt đầu sau dialog tại Target; implementation hook native phải được kiểm thử quyền Accessibility/Screen Recording/Camera theo từng OS trước khi demo.
- `AuditLog` ghi pairing, registration và mỗi relay command tại Gateway.
