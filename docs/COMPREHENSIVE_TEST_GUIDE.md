# 🚀 TÀI LIỆU HƯỚNG DẪN TEST TOÀN DIỆN VÀ CHI TIẾT (E2E)

Tài liệu này hướng dẫn chi tiết cách kiểm thử toàn bộ hệ thống **Remote Control LAN** trong 5 kịch bản kiểm thử khác nhau (gồm tự test trên 1 máy Mac, 1 máy Win và các kịch bản điều khiển qua mạng LAN).

---

## 📚 KIẾN THỨC NỀN TẢNG

### Hệ thống gồm 3 thành phần bắt buộc:

| Thành phần | Vai trò | Cần cài đặt |
|---|---|---|
| **Gateway** (Máy chủ) | Trung tâm xử lý API, WebSocket, cơ sở dữ liệu | .NET 8 SDK |
| **Web Client** (Giao diện) | Trang web cho người điều khiển (Operator) | Node.js (v18+) |
| **Agent** (Máy bị điều khiển) | Chạy ngầm trên máy Target, nhận lệnh từ Gateway | .NET 8 SDK |

### Quy trình hoạt động tổng quát (áp dụng cho mọi kịch bản):

```
Bước 1: Khởi động Gateway (máy chủ)
Bước 2: Khởi động Web Client → Mở trình duyệt → Đăng nhập bằng tài khoản admin mặc định
Bước 3: Tạo Agent trực tiếp trên giao diện Web (nút "+ Tạo Agent")
Bước 4: Cấu hình Agent (dán AgentId + Secret vào file cấu hình)
Bước 5: Khởi động Agent → Agent in ra mã PIN 6 số (tự động làm mới sau mỗi 4 phút)
Bước 6: Quay lại Web → Chọn Agent → Nhập PIN → Bắt đầu điều khiển
```

---

## 💻 KỊCH BẢN 1: TỰ TEST TRÊN 1 MÁY MACBOOK (Localhost)

| Thông tin | Chi tiết |
|---|---|
| **Số máy tính** | 1 máy Mac duy nhất |
| **Số terminal cần mở** | 3 cửa sổ Terminal riêng biệt |
| **Yêu cầu cài đặt** | .NET 8 SDK, Node.js (v18+), mã nguồn dự án |

### Bước 1 — Terminal 1: Khởi động Gateway

**Trước khi chạy lần đầu**, kiểm tra file `src/Gateway/appsettings.Local.json` có tồn tại không. Nếu chưa có (do bị `.gitignore`), hãy tạo file với nội dung:

```json
{
  "Jwt": {
    "Key": "RemoteControlLAN-SuperSecretKey-2026-DoAnMonHoc!@#$"
  }
}
```

Sau đó chạy Gateway:

```bash
cd "/Users/phamgiahung/Downloads/ĐỒ ÁN MÔN HỌC/MẠNG MÁY TÍNH/PROJECT VIPPRO/src/Gateway"
dotnet run
```

**Kết quả mong đợi:** Terminal hiện dòng chữ `Now listening on: http://localhost:5000`. Gateway cũng sẽ tự tạo tài khoản admin mặc định (`admin` / `Admin@123`) nếu chưa có, và tự mở khoá nếu tài khoản bị khoá do đăng nhập sai nhiều lần. Giữ nguyên terminal này, **không được tắt**.

**Kiểm tra:** Mở trình duyệt, truy cập `http://localhost:5000/health`. Nếu thấy `{"status":"ok"}` nghĩa là Gateway đã sẵn sàng.

### Bước 2 — Terminal 2: Khởi động Web Client

```bash
cd "/Users/phamgiahung/Downloads/ĐỒ ÁN MÔN HỌC/MẠNG MÁY TÍNH/PROJECT VIPPRO/src/WebClient"
npm run dev
```

**Kết quả mong đợi:** Terminal hiện dòng `Local: http://localhost:5173/`. Giữ nguyên terminal này.

**Thao tác trên trình duyệt:**
1. Mở `http://localhost:5173` trên trình duyệt.
2. Bạn sẽ thấy màn hình **"Đăng nhập Operator"**.
3. Bấm đăng nhập với tài khoản Admin mặc định:
   - Username: `admin`
   - Password: `Admin@123`
4. (Hệ thống đã tự động tạo sẵn tài khoản Admin này lúc khởi động Gateway).
5. Sau khi đăng nhập, hệ thống sẽ chuyển sang trang Dashboard. Ở góc phải, bạn sẽ thấy nút **"Admin Panel"** dùng để quản lý toàn bộ hệ thống.

### Bước 3 — Tạo Agent trên giao diện Web

1. Ngay trên trang Dashboard, bấm nút **"+ Tạo Agent"**.
2. Nhập tên máy (ví dụ: `MacBook-Test`) và chọn hệ điều hành `MacOS`.
3. Bấm **"Tạo mới"**.
4. Hệ thống sẽ hiển thị một bảng màu xanh chứa `AgentId` và `AgentSecretKey`.

> ⚠️ **QUAN TRỌNG:** `AgentSecretKey` chỉ hiển thị **DUY NHẤT 1 LẦN**. Hãy để nguyên bảng này trên màn hình để copy sang Bước 4.

### Bước 4 — Cấu hình Agent

Mở file `src/Agent/appsettings.json` và sửa 3 dòng sau:

```json
{
  "Agent": {
    "GatewayUrl": "ws://localhost:5000/ws",
    "AgentId": "<DÁN agentId VÀO ĐÂY>",
    "AgentSecretKey": "<DÁN agentSecretKey VÀO ĐÂY>",
    "AllowPowerCommands": false,
    "AdditionalBlockedPaths": [],
    "AdditionalProtectedProcesses": []
  }
}
```

### Bước 5 — Terminal 3 (tiếp): Khởi động Agent

```bash
cd "/Users/phamgiahung/Downloads/ĐỒ ÁN MÔN HỌC/MẠNG MÁY TÍNH/PROJECT VIPPRO/src/Agent"
dotnet run
```

**Kết quả mong đợi:** Terminal in ra dòng chứa **mã PIN 6 số** (ví dụ: `PIN: 482917`). Ghi lại mã này.

> **Tính năng mới:** Mã PIN này có hiệu lực trong 5 phút. Sau mỗi 3 phút, Agent sẽ tự động sinh mã PIN mới và cập nhật lên Gateway. Bạn không cần phải khởi động lại Agent nếu lỡ để quá thời gian!

### Bước 6 — Ghép cặp trên trình duyệt

1. Quay lại trình duyệt (`http://localhost:5173`).
2. Bấm nút **"Làm mới danh sách"** → Agent vừa tạo sẽ tự động được chọn.
3. Nhập mã PIN 6 số vừa ghi ở Bước 5.
4. Bấm **"Kết nối"** → Nếu thành công, trang sẽ chuyển sang giao diện điều khiển.

---

## 💻 KỊCH BẢN 2: TỰ TEST TRÊN 1 MÁY WINDOWS (Localhost)

| Thông tin | Chi tiết |
|---|---|
| **Số máy tính** | 1 máy Windows duy nhất |
| **Số Terminal / Command Prompt** | 3 cửa sổ riêng biệt |
| **Yêu cầu cài đặt** | .NET 8 SDK, Node.js (v18+), mã nguồn dự án |

### Bước 1 — Terminal 1 (cmd / PowerShell): Khởi động Gateway

**Trước khi chạy lần đầu**, kiểm tra file `src\Gateway\appsettings.Local.json` có tồn tại không. Nếu chưa có, tạo file với nội dung:

```json
{
  "Jwt": {
    "Key": "RemoteControlLAN-SuperSecretKey-2026-DoAnMonHoc!@#$"
  }
}
```

Sau đó chạy Gateway:

```cmd
cd src\Gateway
dotnet run
```

**Kết quả mong đợi:** Terminal hiện dòng `Now listening on: http://localhost:5000`.

### Bước 2 — Terminal 2: Khởi động Web Client

```cmd
cd src\WebClient
npm run dev
```

Mở trình duyệt `http://localhost:5173` ➔ Đăng nhập bằng `admin` / `Admin@123`.

### Bước 3 — Tạo Agent trên giao diện Web

1. Nhấn nút **"+ Tạo Agent"**.
2. Nhập tên máy (ví dụ: `Windows-Test`) và chọn Platform là **`Windows`**.
3. Bấm **"Tạo mới"** và lưu lại `AgentId` + `AgentSecretKey`.

### Bước 4 — Cấu hình Agent

Mở file `src\Agent\appsettings.json` dán `AgentId` và `AgentSecretKey` vừa tạo.

### Bước 5 — Terminal 3: Khởi động Agent

```cmd
cd src\Agent
dotnet run
```

**Kết quả mong đợi:** Màn hình Terminal in mã **PIN 6 số**.

### Bước 6 — Ghép cặp trên trình duyệt

Nhập mã PIN vào Web Client (`http://localhost:5173`) và bấm **"Kết nối"**.

---

## 🖥 KỊCH BẢN 3: WINDOWS ĐIỀU KHIỂN MACBOOK (Mạng LAN)

| Thông tin | Chi tiết |
|---|---|
| **Số máy tính** | 2 máy (cùng mạng WiFi/LAN) |
| **Máy Windows (Operator)** | Mở 2 terminal — Chạy Gateway + Web Client |
| **Máy MacBook (Target)** | Mở 1 terminal — Chạy Agent |
| **Yêu cầu cài đặt (Win)** | .NET 8 SDK, Node.js |
| **Yêu cầu cài đặt (Mac)** | .NET 8 SDK |

### TRÊN MÁY WINDOWS (OPERATOR)

**1. Tìm IP mạng LAN:**
Mở Command Prompt (cmd), gõ `ipconfig`. Tìm dòng `IPv4 Address` (ví dụ: `192.168.1.15`).

**2. Mở Firewall cho cổng 5000:**
Mở PowerShell dưới quyền **Administrator** và chạy:
```powershell
netsh advfirewall firewall add rule name="Allow Port 5000" dir=in action=allow protocol=TCP localport=5000
```

**3. Terminal 1 (Windows) — Chạy Gateway:**
```bash
cd src\Gateway
dotnet run --urls "http://0.0.0.0:5000"
```
> Phải dùng `0.0.0.0` thay vì `localhost` để máy khác trong LAN truy cập được.

**4. Terminal 2 (Windows) — Chạy Web Client:**
- Sửa file `src\WebClient\.env.local`:
  ```
  VITE_GATEWAY_HTTP_URL=http://192.168.1.15:5000
  ```
- Chạy:
  ```bash
  cd src\WebClient
  npm run dev -- --host
  ```
- Mở trình duyệt `http://localhost:5173` → Đăng nhập bằng tài khoản admin mặc định (`admin` / `Admin@123`).

**5. Tạo Agent trên giao diện Web:**
Trên Web Client, bấm nút "+ Tạo Agent". Chọn Platform là `"MacOS"`.
Gửi `agentId` và `agentSecretKey` sang máy Mac (qua chat, email, USB...).

### TRÊN MÁY MACBOOK (TARGET)

**1. Terminal 1 (MacBook) — Cấu hình và chạy Agent:**
- Sửa `src/Agent/appsettings.json`:
  ```json
  "GatewayUrl": "ws://192.168.1.15:5000/ws",
  "AgentId": "<ID từ máy Windows>",
  "AgentSecretKey": "<Secret từ máy Windows>"
  ```
- Chạy:
  ```bash
  cd src/Agent
  dotnet run
  ```
- Đọc mã PIN → Gửi cho người ở máy Windows nhập vào trình duyệt.

---

## 🍏 KỊCH BẢN 4: MACBOOK ĐIỀU KHIỂN WINDOWS (Mạng LAN)

| Thông tin | Chi tiết |
|---|---|
| **Số máy tính** | 2 máy (cùng mạng WiFi/LAN) |
| **Máy MacBook (Operator)** | Mở 2 terminal — Chạy Gateway + Web Client |
| **Máy Windows (Target)** | Mở 1 terminal — Chạy Agent |
| **Yêu cầu cài đặt (Mac)** | .NET 8 SDK, Node.js |
| **Yêu cầu cài đặt (Win)** | .NET 8 SDK (hoặc .NET 8 Runtime) |

### TRÊN MÁY MACBOOK (OPERATOR)

**1. Tìm IP mạng LAN:**
```bash
ipconfig getifaddr en0
```
(Ví dụ: `192.168.1.20`). Mac mặc định không chặn cổng, không cần chỉnh Firewall.

**2. Terminal 1 (MacBook) — Chạy Gateway:**
```bash
cd src/Gateway
dotnet run --urls "http://0.0.0.0:5000"
```

**3. Terminal 2 (MacBook) — Chạy Web Client:**
- Sửa `src/WebClient/.env.local`:
  ```
  VITE_GATEWAY_HTTP_URL=http://192.168.1.20:5000
  ```
- Chạy:
  ```bash
  cd src/WebClient
  npm run dev -- --host
  ```
- Mở trình duyệt → Đăng nhập bằng tài khoản admin mặc định (`admin` / `Admin@123`).

**4. Tạo Agent trên giao diện Web:**
Tương tự Bước 3 ở Kịch bản 1, bấm "+ Tạo Agent" trên web, chọn Platform là `"Windows"`. Gửi credentials sang máy Win.

### TRÊN MÁY WINDOWS (TARGET)

**1. Terminal 1 (Windows) — Cấu hình và chạy Agent:**
- Sửa `src\Agent\appsettings.json`:
  ```json
  "GatewayUrl": "ws://192.168.1.20:5000/ws",
  "AgentId": "<ID từ máy Mac>",
  "AgentSecretKey": "<Secret từ máy Mac>"
  ```
- Chạy: `dotnet run`
- Đọc mã PIN → Gửi cho người ở máy MacBook nhập vào trình duyệt.

> **Lưu ý Windows:** Nếu Windows Defender báo chặn, bấm **"Allow access"**. Keylogger có thể bị Antivirus phát hiện → cần thêm exception.

---

## 🖥 KỊCH BẢN 5: WINDOWS ĐIỀU KHIỂN WINDOWS (Mạng LAN)

| Thông tin | Chi tiết |
|---|---|
| **Số máy tính** | 2 máy Windows (cùng mạng WiFi/LAN) |
| **Máy Windows A (Operator)** | Mở 2 terminal — Chạy Gateway + Web Client |
| **Máy Windows B (Target)** | Mở 1 terminal — Chạy Agent |
| **Yêu cầu cài đặt (Máy A)** | .NET 8 SDK, Node.js |
| **Yêu cầu cài đặt (Máy B)** | .NET 8 SDK (hoặc Runtime) |

### TRÊN MÁY WINDOWS A (OPERATOR)

Thực hiện giống hệt phần "TRÊN MÁY WINDOWS (OPERATOR)" ở Kịch bản 3:
1. Tìm IP bằng `ipconfig` (ví dụ: `192.168.1.50`).
2. Mở Firewall cổng 5000.
3. Terminal 1: Chạy Gateway với `--urls "http://0.0.0.0:5000"`.
4. Terminal 2: Sửa `.env.local` → Chạy Web Client → Đăng nhập (`admin` / `Admin@123`).
5. Tạo Agent qua nút "+ Tạo Agent" trên Web (chọn Platform `"Windows"`), gửi credentials sang máy B.

### TRÊN MÁY WINDOWS B (TARGET)

1. Sửa `appsettings.json`: đặt `GatewayUrl` trỏ về IP máy A.
2. Chạy `dotnet run`.
3. Đọc mã PIN → Gửi cho máy A nhập.

---

## 📋 QUY TRÌNH TEST CÁC TÍNH NĂNG

Sau khi ghép cặp thành công (trang chuyển sang giao diện điều khiển), hãy test lần lượt:

| # | Tính năng | Cách test | Kết quả đúng |
|---|---|---|---|
| 1 | **Danh sách tiến trình** | Click tab "Processes" | Hiện danh sách tất cả tiến trình đang chạy trên máy Target |
| 2 | **Mở ứng dụng** | Gõ `notepad` (Win) hoặc `open -a Calculator` (Mac) vào ô Start Process → Bấm Start | App tương ứng tự mở trên máy Target |
| 3 | **Tắt ứng dụng** | Chọn app vừa mở → Bấm "Kill" | App bị đóng lập tức (nếu chọn app hệ thống như `kernel_task` hoặc `svchost` sẽ báo lỗi cảnh báo bảo mật) |
| 4 | **Xem màn hình** | Click tab "Screen" | Hiện ảnh chụp desktop máy Target, cập nhật liên tục |
| 5 | **Webcam** | Click tab "Webcam" | Đèn camera trên máy Target sáng, hình ảnh truyền lên Web |
| 6 | **Duyệt file & Upload** | Click tab "Files", chọn tệp upload ➔ Xem khung Preview ➔ Bấm **Xác nhận Upload** | Tệp truyền sang Target thành công |
| 7 | **Keylogger** | Bật Keylogger → Sang máy Target gõ bàn phím → Tắt & bật lại | Ký tự xuất hiện real-time trên Web, bật/tắt lại mượt mà |
| 8 | **Ngắt kết nối** | Nhấn `Ctrl + C` trên Terminal Agent | Web hiển thị trạng thái Offline ngay lập tức |
| 9 | **Vi phạm bảo mật quá 5 lần** | Thử mở thư mục cấm (ví dụ `/System` hoặc `C:\Windows`) 5 lần | Cảnh báo xuất hiện trên Web & Máy Target. Lần 5 Agent tự động tắt để bảo vệ máy Target. |

### Lưu ý quyền trên macOS (khi máy Target là Mac):
- **Screen View:** Cần cấp quyền *System Settings → Privacy & Security → Screen Recording* cho Terminal.
- **Webcam:** Cần cấp quyền *Camera* cho Terminal.
- **Keylogger:** Cần cấp quyền *Accessibility* cho Terminal.

### Lưu ý trên Windows (khi máy Target là Windows):
- **Keylogger:** Có thể bị Windows Defender/Antivirus chặn → cần thêm exception.
- **Kill Process:** Tiến trình SYSTEM sẽ bị từ chối (Access Denied) → đây là hành vi đúng.

---

## ❓ XỬ LÝ SỰ CỐ THƯỜNG GẶP

| Sự cố | Nguyên nhân | Cách khắc phục |
|---|---|---|
| `http://localhost:5000/health` không trả về gì | Gateway chưa chạy hoặc bị lỗi | Kiểm tra Terminal 1, nếu có lỗi "Address in use" → `Ctrl + C` rồi `dotnet run` lại |
| Lỗi "Jwt:Key phải có ít nhất 32 ký tự" | Thiếu file `appsettings.Local.json` | Tạo file `src/Gateway/appsettings.Local.json` với JWT Key (xem Bước 1 Kịch bản 1) |
| Đăng nhập admin báo "Tài khoản đang bị khoá" | Đăng nhập sai nhiều lần trước đó | Restart Gateway (`Ctrl+C` → `dotnet run` lại) — hệ thống tự mở khoá admin khi khởi động |
| Mã PIN liên tục hiện ra mới | Do tự động cập nhật mỗi 4 phút | Cứ sử dụng mã PIN mới nhất hiển thị dưới cùng của Terminal. |
| Vào web thấy trang trắng hoặc lỗi | Web Client chưa chạy hoặc `.env.local` sai URL | Kiểm tra Terminal 2 có đang chạy không, kiểm tra URL trong `.env.local` |
| Bấm "Làm mới" không thấy Agent | Chưa tạo Agent hoặc token hết hạn | Hãy bấm "+ Tạo Agent". Nếu token hết hạn, trang sẽ tự chuyển về Đăng nhập |
| Nhập PIN báo "Agent đang ngoại tuyến" | Agent chưa chạy hoặc đã tắt | Kiểm tra Terminal Agent có đang chạy không |
| Máy khác trong LAN không kết nối được | Firewall chặn hoặc sai IP | Kiểm tra đã mở port 5000, dùng `--urls "http://0.0.0.0:5000"` khi chạy Gateway |
