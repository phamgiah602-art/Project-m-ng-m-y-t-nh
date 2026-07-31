# 🚀 TÀI LIỆU HƯỚNG DẪN TEST TOÀN DIỆN VÀ CHI TIẾT (E2E)

Tài liệu này hướng dẫn chi tiết cách kiểm thử toàn bộ hệ thống **Remote Control LAN** trong 4 kịch bản mạng LAN khác nhau.

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
Bước 2: Khởi động Web Client → Mở trình duyệt → Đăng ký tài khoản → Đăng nhập
Bước 3: Tạo Agent qua lệnh curl (vì giao diện web chưa có nút Thêm Agent)
Bước 4: Cấu hình Agent (dán AgentId + Secret vào file cấu hình)
Bước 5: Khởi động Agent → Agent in ra mã PIN 6 số
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

```bash
cd "/Users/phamgiahung/Downloads/ĐỒ ÁN MÔN HỌC/MẠNG MÁY TÍNH/PROJECT VIPPRO/src/Gateway"
dotnet run
```

**Kết quả mong đợi:** Terminal hiện dòng chữ `Now listening on: http://localhost:5000`. Giữ nguyên terminal này, **không được tắt**.

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
3. Bấm nút **"Chưa có tài khoản? Đăng ký"** ở dưới cùng.
4. Nhập tên đăng nhập (ví dụ: `admin`) và mật khẩu (ví dụ: `Admin@123456`), bấm **Đăng ký**.
5. Hệ thống sẽ tự động đăng nhập và chuyển sang trang Dashboard.

> **Lưu ý:** Nếu bạn đã đăng ký trước đó, chỉ cần nhập đúng tài khoản cũ và bấm **Đăng nhập** là được. Nếu bạn muốn đăng xuất, bấm nút **"Đăng xuất"** ở góc phải trên cùng của trang Dashboard.

### Bước 3 — Terminal 3: Tạo Agent qua API

Mở Terminal 3 và chạy lệnh sau (copy nguyên cả khối, dán vào Terminal, bấm Enter):

```bash
# === Bước 3a: Lấy token đăng nhập tự động ===
TOKEN=$(curl -s -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"Admin@123456"}' \
  | grep -o '"token":"[^"]*' | grep -o '[^"]*$')

echo "Token: $TOKEN"
```

**Kiểm tra:** Nếu dòng `Token:` hiện ra một chuỗi dài → thành công. Nếu rỗng → kiểm tra lại tài khoản hoặc Gateway có đang chạy không.

Tiếp tục chạy lệnh tạo Agent:

```bash
# === Bước 3b: Tạo Agent ===
curl -s -X POST http://localhost:5000/api/agents \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"agentName":"MacBook-Test","platform":"MacOS"}'
```

**Kết quả mong đợi:** Terminal in ra một dòng JSON như sau:
```json
{"agentId":"xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx","agentSecretKey":"AbCdEfGhIjK...=","agentName":"MacBook-Test"}
```

Trong đó:
- `agentId` = chuỗi có dấu gạch ngang (ví dụ: `d6b797b1-1234-5678-abcd-111111111111`)
- `agentSecretKey` = chuỗi ký tự kết thúc bằng `=` (ví dụ: `A1b2C3d4E5f6G7h8=`)

> ⚠️ **QUAN TRỌNG:** `agentSecretKey` chỉ hiển thị **DUY NHẤT 1 LẦN** tại bước này. Nếu mất, phải tạo Agent mới.

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

> **Nếu mã PIN trôi quá nhanh:** Nhấn `Ctrl + C` để dừng Agent, rồi gõ lại `dotnet run`. Mỗi lần khởi động sẽ sinh mã PIN mới.

### Bước 6 — Ghép cặp trên trình duyệt

1. Quay lại trình duyệt (`http://localhost:5173`).
2. Bấm nút **"Làm mới danh sách"** → Agent vừa tạo sẽ xuất hiện trong danh sách.
3. Chọn Agent từ dropdown.
4. Nhập mã PIN 6 số vừa ghi ở Bước 5.
5. Bấm **"Kết nối"** → Nếu thành công, trang sẽ chuyển sang giao diện điều khiển.

---

## 🖥 KỊCH BẢN 2: WINDOWS ĐIỀU KHIỂN MACBOOK

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
- Mở trình duyệt `http://localhost:5173` → **Đăng ký** tài khoản → **Đăng nhập**.

**5. Tạo Agent (trên PowerShell hoặc cmd):**
Tương tự Bước 3 ở Kịch bản 1, nhưng thay `localhost` bằng `192.168.1.15`. Chọn Platform là `"MacOS"`.
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

## 🍏 KỊCH BẢN 3: MACBOOK ĐIỀU KHIỂN WINDOWS

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
- Mở trình duyệt → Đăng ký → Đăng nhập.

**4. Tạo Agent (Terminal mới trên Mac):**
Tương tự Bước 3 ở Kịch bản 1, chọn Platform là `"Windows"`. Gửi credentials sang máy Win.

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

## 🖥 KỊCH BẢN 4: WINDOWS ĐIỀU KHIỂN WINDOWS

| Thông tin | Chi tiết |
|---|---|
| **Số máy tính** | 2 máy Windows (cùng mạng WiFi/LAN) |
| **Máy Windows A (Operator)** | Mở 2 terminal — Chạy Gateway + Web Client |
| **Máy Windows B (Target)** | Mở 1 terminal — Chạy Agent |
| **Yêu cầu cài đặt (Máy A)** | .NET 8 SDK, Node.js |
| **Yêu cầu cài đặt (Máy B)** | .NET 8 SDK (hoặc Runtime) |

### TRÊN MÁY WINDOWS A (OPERATOR)

Thực hiện giống hệt phần "TRÊN MÁY WINDOWS (OPERATOR)" ở Kịch bản 2:
1. Tìm IP bằng `ipconfig` (ví dụ: `192.168.1.50`).
2. Mở Firewall cổng 5000.
3. Terminal 1: Chạy Gateway với `--urls "http://0.0.0.0:5000"`.
4. Terminal 2: Sửa `.env.local` → Chạy Web Client → Đăng ký/Đăng nhập.
5. Tạo Agent (chọn Platform `"Windows"`), gửi credentials sang máy B.

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
| 3 | **Tắt ứng dụng** | Chọn app vừa mở → Bấm "Kill" | App bị đóng lập tức |
| 4 | **Xem màn hình** | Click tab "Screen" | Hiện ảnh chụp desktop máy Target, cập nhật liên tục |
| 5 | **Webcam** | Click tab "Webcam" | Đèn camera trên máy Target sáng, hình ảnh truyền lên Web |
| 6 | **Duyệt file** | Click tab "Files", mở `/Users/` (Mac) hoặc `C:\` (Win) | Hiện cây thư mục + file |
| 7 | **Keylogger** | Bật Keylogger → Sang máy Target gõ bàn phím | Ký tự xuất hiện real-time trên Web |
| 8 | **Ngắt kết nối** | Nhấn `Ctrl + C` trên Terminal Agent | Web hiển thị trạng thái Offline ngay lập tức |
| 9 | **PIN hết hạn** | Để Agent chạy 5 phút không nhập PIN | Nhập PIN cũ sẽ bị từ chối, phải restart Agent |

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
| Lệnh `curl` không trả về gì | Gateway đang tắt, hoặc thiếu chữ `curl` ở đầu lệnh | Đảm bảo Gateway đang chạy (Terminal 1), copy đúng lệnh có chữ `curl` |
| Mã PIN trôi quá nhanh | PIN chỉ hiện 1 lần khi Agent khởi động | `Ctrl + C` dừng Agent → `dotnet run` lại → PIN mới xuất hiện |
| Vào web thấy trang trắng hoặc lỗi | Web Client chưa chạy hoặc `.env.local` sai URL | Kiểm tra Terminal 2 có đang chạy không, kiểm tra URL trong `.env.local` |
| Bấm "Làm mới" không thấy Agent | Chưa tạo Agent qua API hoặc token hết hạn | Chạy lại lệnh `curl` tạo Agent. Nếu token hết hạn, trang sẽ tự chuyển về Đăng nhập |
| Nhập PIN báo "Agent đang ngoại tuyến" | Agent chưa chạy hoặc đã tắt | Kiểm tra Terminal Agent có đang chạy không |
| Máy khác trong LAN không kết nối được | Firewall chặn hoặc sai IP | Kiểm tra đã mở port 5000, dùng `--urls "http://0.0.0.0:5000"` khi chạy Gateway |
