# 🚀 TÀI LIỆU HƯỚNG DẪN TEST TOÀN DIỆN VÀ CHI TIẾT (E2E)

Tài liệu này hướng dẫn chi tiết cách kiểm thử toàn bộ hệ thống **Remote Control LAN** theo **6 kịch bản** kiểm thử khác nhau. Ở mỗi kịch bản, bạn đều có 2 lựa chọn (Option) để khởi chạy hệ thống: dùng Docker hoặc dùng Terminal.

---

## 🛠 YÊU CẦU CÀI ĐẶT (PREREQUISITES)

Tuỳ thuộc vào lựa chọn chạy bằng **Docker** hay **Terminal**, các thành phần của hệ thống (Web Client, Gateway, Agent) sẽ yêu cầu cài đặt môi trường khác nhau:

### 1. Nếu chạy bằng Docker (Dành cho Gateway & Web Client)
*Khuyên dùng cho phía máy điều khiển (Operator) để tiết kiệm thời gian setup.*

| Thành phần | Môi trường cần cài đặt | Ghi chú |
|---|---|---|
| **Web Client** | Docker Desktop | Chạy trong container `rclan-webclient`. |
| **Gateway** | Docker Desktop | Chạy trong container `rclan-gateway`. |
| **Agent** | .NET 8 SDK | **Bắt buộc chạy trực tiếp (Terminal)** vì Agent cần quyền thao tác sâu vào hệ điều hành (chụp màn hình, quản lý tiến trình, keylogger...). |

### 2. Nếu chạy hoàn toàn bằng Terminal (Local)
*Dành cho việc dev hoặc khi không có sẵn Docker.*

| Thành phần | Môi trường cần cài đặt | Ghi chú |
|---|---|---|
| **Web Client** | Node.js (v18 trở lên) | Dùng lệnh `npm install` và `npm run dev`. |
| **Gateway** | .NET 8 SDK | Dùng lệnh `dotnet run`. |
| **Agent** | .NET 8 SDK | Dùng lệnh `dotnet run`. |

---

## ⚙️ CẤU HÌNH CHUNG BAN ĐẦU

### Dành cho Option Terminal (Chạy Gateway bằng `dotnet run`)

Trước khi chạy Gateway bằng Terminal, hãy tạo file Secret Key:

1. Đi tới thư mục `src/Gateway/`.
2. Tạo file `appsettings.Local.json` với nội dung:
```json
{
  "Jwt": {
    "Key": "RemoteControlLAN-SuperSecretKey-2026-DoAnMonHoc!@#$"
  }
}
```

> **Lưu ý:** Nếu bạn chọn chạy bằng **Docker** (Option 1), hãy **bỏ qua bước này** — JWT Key đã được cấu hình sẵn trong file `docker-compose.yml`.

### Cấu hình Agent (`src/Agent/appsettings.json`)

Sau khi tạo Agent trên giao diện Web, bạn sẽ cần cập nhật file `appsettings.json` của Agent với các thông tin sau:

```json
{
  "Agent": {
    "GatewayUrl": "ws://<ĐỊA_CHỈ_GATEWAY>:<CỔNG>/ws",
    "AgentId": "<AGENT_ID_TỪ_WEB>",
    "AgentSecretKey": "<SECRET_KEY_TỪ_WEB>",
    "AllowPowerCommands": true,
    "AdditionalBlockedPaths": [],
    "AdditionalProtectedProcesses": []
  }
}
```

> **Ghi chú:** `AllowPowerCommands` mặc định là `false`. Đặt thành `true` nếu bạn muốn cho phép Operator gửi lệnh **Tắt máy (Shutdown)** hoặc **Khởi động lại (Restart)** máy Target từ xa.

---

## 📋 6 KỊCH BẢN KIỂM THỬ (TEST SCENARIOS)

Dưới đây là 6 kịch bản. Quy ước:
- **Operator (Người điều khiển):** Chạy Gateway + Web Client.
- **Target / Agent (Máy bị điều khiển):** Chạy Agent.

*(Tài khoản đăng nhập Web mặc định: `admin` / `Admin@123`)*

---

### KỊCH BẢN 1: TỰ TEST TRÊN 1 MÁY WINDOWS (Localhost)
*Mọi thành phần đều chạy trên cùng 1 máy tính Windows.*

#### Option 1: Chạy bằng Docker
1. **Khởi động Gateway & Web Client:**
   - Mở Terminal ở thư mục gốc, chạy: `docker compose up --build`
2. **Khởi động Agent:**
   - Đăng nhập Web Client (`http://localhost:5173`), tạo Agent (chọn HĐH Windows), lấy `AgentId` và `AgentSecretKey`.
   - Cấu hình file `src\Agent\appsettings.json`, đặt `GatewayUrl` là `ws://localhost:5001/ws`.
   - Mở Terminal mới, vào `src\Agent` chạy `dotnet run`.
   - Nhập mã PIN vào Web Client để kết nối.

#### Option 2: Chạy bằng Terminal
1. **Khởi động Gateway & Web Client:**
   - Terminal 1 (Gateway): `cd src\Gateway` ➔ `dotnet run` (Chạy cổng 5000).
   - Terminal 2 (Web Client): `cd src\WebClient` ➔ `npm install` (lần đầu) ➔ `npm run dev` (Chạy cổng 5173).
2. **Khởi động Agent:**
   - Đăng nhập Web (`http://localhost:5173`), tạo Agent (chọn HĐH Windows), lấy ID/Secret.
   - Cấu hình file `src\Agent\appsettings.json`, đặt `GatewayUrl` là `ws://localhost:5000/ws`.
   - Terminal 3 (Agent): `cd src\Agent` ➔ `dotnet run`.
   - Nhập mã PIN vào Web Client để kết nối.

---

### KỊCH BẢN 2: TỰ TEST TRÊN 1 MÁY MAC (Localhost)
*Mọi thành phần đều chạy trên cùng 1 máy tính MacBook.*

#### Option 1: Chạy bằng Docker
1. **Khởi động Gateway & Web Client:**
   - Mở Terminal ở thư mục gốc, chạy: `docker compose up --build`
2. **Khởi động Agent:**
   - Đăng nhập Web Client (`http://localhost:5173`), tạo Agent (chọn HĐH MacOS), lấy ID/Secret.
   - Cấu hình file `src/Agent/appsettings.json`, đặt `GatewayUrl` là `ws://localhost:5001/ws`.
   - Mở Terminal mới, vào `src/Agent` chạy `dotnet run`.
   - Nhập mã PIN vào Web Client để kết nối.

#### Option 2: Chạy bằng Terminal
1. **Khởi động Gateway & Web Client:**
   - Terminal 1 (Gateway): `cd src/Gateway` ➔ `dotnet run` (Chạy cổng 5000).
   - Terminal 2 (Web Client): `cd src/WebClient` ➔ `npm install` (lần đầu) ➔ `npm run dev` (Chạy cổng 5173).
2. **Khởi động Agent:**
   - Đăng nhập Web (`http://localhost:5173`), tạo Agent (chọn HĐH MacOS), lấy ID/Secret.
   - Cấu hình file `src/Agent/appsettings.json`, đặt `GatewayUrl` là `ws://localhost:5000/ws`.
   - Terminal 3 (Agent): `cd src/Agent` ➔ `dotnet run`.
   - Nhập mã PIN vào Web Client để kết nối.

---

### KỊCH BẢN 3: MAC (Operator) ĐIỀU KHIỂN WINDOWS (Target)
*Máy Mac quản lý Gateway & Web Client. Máy Windows chạy Agent. (Hai máy chung mạng LAN)*

**Trên máy Mac (Operator):**
*Tìm IP LAN của Mac bằng lệnh: `ipconfig getifaddr en0` (Giả sử là `192.168.1.20`)*

#### Option 1: Chạy bằng Docker (trên Mac)
- Chạy `docker compose up --build` tại thư mục gốc.
- Đăng nhập `http://localhost:5173` tạo Agent (Windows), lấy credentials gửi cho máy Windows.
- Cấu hình Agent trên Windows: `GatewayUrl` là `ws://192.168.1.20:5001/ws`.

#### Option 2: Chạy bằng Terminal (trên Mac)
- Terminal 1: `cd src/Gateway` ➔ `dotnet run --urls "http://0.0.0.0:5000"`
- Terminal 2: Sửa file `src/WebClient/.env.local` thêm `VITE_GATEWAY_HTTP_URL=http://192.168.1.20:5000` ➔ `cd src/WebClient` ➔ `npm install` (lần đầu) ➔ `npm run dev -- --host`
- Đăng nhập Web, tạo Agent (Windows), gửi credentials cho Windows.
- Cấu hình Agent trên Windows: `GatewayUrl` là `ws://192.168.1.20:5000/ws`.

**Trên máy Windows (Target):**
- Chỉnh sửa `src\Agent\appsettings.json` bằng thông tin nhận được (URL, ID, Secret).
- Mở Terminal chạy: `cd src\Agent` ➔ `dotnet run`.
- Đọc mã PIN đưa cho người dùng Mac kết nối. *(Lưu ý: Nếu Windows Defender chặn, hãy bấm Allow).*

---

### KỊCH BẢN 4: WINDOWS (Operator) ĐIỀU KHIỂN MAC (Target)
*Máy Windows quản lý Gateway & Web Client. Máy Mac chạy Agent. (Hai máy chung mạng LAN)*

**Trên máy Windows (Operator):**
*Tìm IP LAN bằng lệnh: `ipconfig` (Giả sử là `192.168.1.15`)*
*(Yêu cầu: Mở Firewall cổng 5000, 5001 trên Windows trước khi chạy)*

#### Option 1: Chạy bằng Docker (trên Windows)
- Chạy `docker compose up --build`.
- Đăng nhập web `http://localhost:5173`, tạo Agent (MacOS), gửi credentials cho Mac.
- Cấu hình Agent trên Mac: `GatewayUrl` là `ws://192.168.1.15:5001/ws`.

#### Option 2: Chạy bằng Terminal (trên Windows)
- Terminal 1 (Gateway): `cd src\Gateway` ➔ `dotnet run --urls "http://0.0.0.0:5000"`
- Terminal 2 (Web Client): Sửa `VITE_GATEWAY_HTTP_URL=http://192.168.1.15:5000` trong `.env.local` ➔ `cd src\WebClient` ➔ `npm install` (lần đầu) ➔ `npm run dev -- --host`
- Đăng nhập Web, tạo Agent (MacOS), gửi credentials cho Mac.
- Cấu hình Agent trên Mac: `GatewayUrl` là `ws://192.168.1.15:5000/ws`.

**Trên máy Mac (Target):**
- Chỉnh sửa `src/Agent/appsettings.json` bằng thông tin nhận được.
- Mở Terminal chạy: `cd src/Agent` ➔ `dotnet run`.
- Đọc mã PIN đưa cho Windows. *(Cần cấp quyền Accessibility, Screen Recording cho Terminal trên Mac).*

---

### KỊCH BẢN 5: WINDOWS (Operator) ĐIỀU KHIỂN WINDOWS (Target)
*Máy Windows A điều khiển máy Windows B trong cùng mạng LAN.*

**Trên máy Windows A (Operator):**
*Tìm IP LAN, mở Firewall cổng 5000, 5001.*

#### Option 1: Chạy bằng Docker (trên Windows A)
- Chạy `docker compose up --build`.
- Tạo Agent (Windows) trên web, gửi credentials sang máy B.
- URL Gateway cho máy B là: `ws://<IP_Máy_A>:5001/ws`.

#### Option 2: Chạy bằng Terminal (trên Windows A)
- Gateway: `dotnet run --urls "http://0.0.0.0:5000"`
- Web Client: Sửa `.env.local` trỏ về IP của Máy A, chạy `npm run dev -- --host`.
- Tạo Agent (Windows) trên web, gửi credentials sang máy B.
- URL Gateway cho máy B là: `ws://<IP_Máy_A>:5000/ws`.

**Trên máy Windows B (Target):**
- Cập nhật `src\Agent\appsettings.json` với URL, ID, Secret.
- Chạy `dotnet run` ở thư mục Agent. Cung cấp mã PIN cho Máy A.

---

### KỊCH BẢN 6: MAC (Operator) ĐIỀU KHIỂN MAC (Target)
*Máy Mac A điều khiển máy Mac B trong cùng mạng LAN.*

**Trên máy Mac A (Operator):**
*Tìm IP LAN bằng lệnh `ipconfig getifaddr en0`.*

#### Option 1: Chạy bằng Docker (trên Mac A)
- Chạy `docker compose up --build`.
- Tạo Agent (MacOS) trên web, gửi credentials sang máy B.
- URL Gateway cho máy B là: `ws://<IP_Máy_A>:5001/ws`.

#### Option 2: Chạy bằng Terminal (trên Mac A)
- Gateway: `dotnet run --urls "http://0.0.0.0:5000"`
- Web Client: Sửa `.env.local` trỏ về IP của Máy A, chạy `npm run dev -- --host`.
- Tạo Agent (MacOS) trên web, gửi credentials sang máy B.
- URL Gateway cho máy B là: `ws://<IP_Máy_A>:5000/ws`.

**Trên máy Mac B (Target):**
- Cập nhật `src/Agent/appsettings.json` với URL, ID, Secret.
- Chạy `dotnet run` ở thư mục Agent. Cung cấp mã PIN cho Máy A.

---

## 📋 QUY TRÌNH TEST CÁC TÍNH NĂNG (SAU KHI KẾT NỐI)

| # | Tính năng | Cách test | Kết quả đúng |
|---|---|---|---|
| 1 | **Danh sách tiến trình** | Click tab "Processes" | Hiện danh sách tất cả tiến trình đang chạy trên máy Target |
| 2 | **Mở ứng dụng** | Gõ `notepad` (Win) hoặc `open -a Calculator` (Mac) vào ô Start Process → Bấm Start | App tương ứng tự mở trên máy Target |
| 3 | **Tắt ứng dụng** | Chọn app vừa mở → Bấm "Kill" | App bị đóng lập tức (Tiến trình hệ thống như `kernel_task` hoặc `svchost` sẽ bị bảo vệ và từ chối) |
| 4 | **Xem màn hình** | Click tab "Screen" | Hiện ảnh chụp desktop máy Target, cập nhật liên tục |
| 5 | **Webcam** | Click tab "Webcam" | Đèn camera trên máy Target sáng, hình ảnh truyền lên Web |
| 6 | **Duyệt file & Upload** | Click tab "Files", chọn tệp upload ➔ Xem khung Preview ➔ Bấm **Xác nhận Upload** | Tệp truyền sang Target thành công |
| 7 | **Download file** | Click tab "Files", duyệt tới tệp trên máy Target → Bấm **Download** | Tệp được tải về máy Operator thành công |
| 8 | **Keylogger** | Bật Keylogger → Sang máy Target gõ bàn phím → Tắt & bật lại | Ký tự xuất hiện real-time trên Web (Máy Target sẽ hiện popup xin đồng ý trước khi bật) |
| 9 | **Tắt máy từ xa (Shutdown)** | Bấm nút "Shutdown" trên Web | Máy Target bắt đầu tắt sau 10 giây. *(Cần `AllowPowerCommands: true` trong `appsettings.json`)* |
| 10 | **Khởi động lại từ xa (Restart)** | Bấm nút "Restart" trên Web | Máy Target tự khởi động lại. *(Cần `AllowPowerCommands: true`)* |
| 11 | **Vi phạm bảo mật quá 5 lần** | Thử truy cập thư mục cấm (`/System`, `C:\Windows`) hoặc kill tiến trình hệ thống liên tục 5 lần | Mỗi lần vi phạm hiện cảnh báo trên Web và máy Target. Lần thứ 5, Agent tự động thoát để bảo vệ máy Target |

### Lưu ý quyền trên macOS (khi máy Target là Mac)
- **Xem màn hình:** Cần cấp quyền *System Settings → Privacy & Security → Screen Recording* cho Terminal.
- **Webcam:** Cần cấp quyền *Camera* cho Terminal.
- **Keylogger:** Cần cấp quyền *Accessibility* cho Terminal.

### Lưu ý trên Windows (khi máy Target là Windows)
- **Keylogger:** Có thể bị Windows Defender/Antivirus chặn → cần thêm exception cho thư mục dự án.
- **Kill Process:** Tiến trình SYSTEM sẽ bị từ chối (Access Denied) → đây là hành vi đúng.

## ❓ XỬ LÝ SỰ CỐ THƯỜNG GẶP

| Sự cố | Nguyên nhân | Cách khắc phục |
|---|---|---|
| Lỗi **"Jwt:Key phải có ít nhất 32 ký tự"** | Thiếu file `appsettings.Local.json` | Tạo file `src/Gateway/appsettings.Local.json` với JWT Key (xem mục Cấu hình chung). Nếu dùng Docker thì bỏ qua |
| **Máy khác trong LAN không kết nối được** | Firewall chặn hoặc sai IP | Mở Firewall cho cổng 5000 và 5001. Gateway phải chạy với `--urls "http://0.0.0.0:5000"`. Dùng `ping <IP>` để kiểm tra kết nối |
| **Mã PIN liên tục đổi** | Tính năng bảo mật tự động | PIN tự thay đổi mỗi **3 phút**, mỗi mã có hiệu lực tối đa **5 phút**. Luôn sử dụng mã mới nhất trên Terminal Agent |
| Agent báo **"Gateway từ chối Agent"** | `AgentId` hoặc `AgentSecretKey` sai | Tạo lại Agent mới trên giao diện Web và copy chính xác ID/Secret vào `appsettings.json` |
| **Agent bị treo, không hiện mã PIN** | Không kết nối được tới Gateway | Kiểm tra kết nối mạng bằng `ping <IP_Gateway>`. Đảm bảo Gateway đang chạy và Firewall đã mở |
| **Cổng 5000 bị chiếm trên Mac** | macOS AirPlay Receiver chiếm cổng 5000 | Tắt AirPlay Receiver trong *System Settings → General → AirDrop & Handoff*, hoặc dùng Docker (cổng 5001) |
| **macOS yêu cầu cấp quyền** | Thiếu quyền Accessibility/Screen Recording/Camera | Vào *System Settings → Privacy & Security* → cấp quyền cho Terminal. Khởi động lại Agent sau khi cấp |
| **Windows Defender chặn Agent/Keylogger** | Antivirus phát hiện hành vi keylogger | Thêm exception cho thư mục project trong Windows Security → Virus & threat protection |
| Đăng nhập admin báo **"Tài khoản đang bị khoá"** | Đăng nhập sai nhiều lần trước đó | Khởi động lại Gateway (`Ctrl+C` → `dotnet run` lại hoặc `docker compose restart`) — hệ thống tự mở khoá admin khi khởi động |
| Bấm Shutdown/Restart báo **"Lệnh nguồn đang bị tắt"** | `AllowPowerCommands` đang là `false` | Sửa `appsettings.json` của Agent: đặt `"AllowPowerCommands": true`, rồi khởi động lại Agent |
