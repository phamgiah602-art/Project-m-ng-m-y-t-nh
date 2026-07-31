# 🚀 TÀI LIỆU HƯỚNG DẪN TEST TOÀN DIỆN VÀ CHI TIẾT (E2E)

Tài liệu này cung cấp các bước cài đặt và chạy lệnh cực kỳ chi tiết (từng terminal, từng lệnh gõ) cho 4 kịch bản mạng LAN.

---

## 💻 KỊCH BẢN 1: TỰ TEST TRÊN CHÍNH MACBOOK CỦA BẠN (Localhost)
**Số lượng máy tính:** 1 máy Mac duy nhất.
**Số lượng terminal cần mở:** 3 terminal riêng biệt trên cùng máy Mac này.

### Yêu cầu cài đặt trước:
- Đã cài đặt **.NET 8 SDK** và **Node.js**.
- Đã clone/copy source code dự án về máy.

### Chi tiết các bước thực hiện trên Máy Mac:

#### Terminal 1: Chạy Gateway
1. Mở Terminal 1.
2. Di chuyển vào thư mục Gateway và chạy:
   ```bash
   cd "/Users/phamgiahung/Downloads/ĐỒ ÁN MÔN HỌC/MẠNG MÁY TÍNH/PROJECT VIPPRO/src/Gateway"
   dotnet run
   ```
   *(Gateway sẽ chạy trên `http://localhost:5000`)*

#### Terminal 2: Tạo Agent và chạy Agent
1. Mở Terminal 2.
2. Đăng ký/đăng nhập và tạo Agent (thay đổi token tương ứng) để lấy `AgentId` và `AgentSecretKey`:
   ```bash
   # Lấy Token
   curl -s -X POST http://localhost:5000/api/auth/login \
     -H "Content-Type: application/json" \
     -d '{"username":"admin","password":"Admin@123456"}'
   
   # Tạo Agent bằng Token vừa lấy
   curl -s -X POST http://localhost:5000/api/agents \
     -H "Content-Type: application/json" \
     -H "Authorization: Bearer <NHẬP_TOKEN_VÀO_ĐÂY>" \
     -d '{"agentName":"MacBook-Test","platform":"MacOS"}'
   ```
3. Mở file `src/Agent/appsettings.json`, điền `AgentId` và `AgentSecretKey` vừa nhận được. Đảm bảo `GatewayUrl` là `"ws://localhost:5000/ws"`.
4. Vẫn ở Terminal 2, khởi động Agent:
   ```bash
   cd "/Users/phamgiahung/Downloads/ĐỒ ÁN MÔN HỌC/MẠNG MÁY TÍNH/PROJECT VIPPRO/src/Agent"
   dotnet run
   ```
   *(Ghi lại mã PIN 6 số xuất hiện trên Terminal này)*

#### Terminal 3: Chạy Web Client
1. Mở Terminal 3.
2. Đảm bảo file `src/WebClient/.env.local` có nội dung: `VITE_GATEWAY_HTTP_URL=http://localhost:5000`
3. Khởi động giao diện Web:
   ```bash
   cd "/Users/phamgiahung/Downloads/ĐỒ ÁN MÔN HỌC/MẠNG MÁY TÍNH/PROJECT VIPPRO/src/WebClient"
   npm run dev
   ```
4. Mở trình duyệt truy cập `http://localhost:5173`. Đăng nhập bằng `admin` / `Admin@123456`, chọn Agent, nhập mã PIN và bắt đầu điều khiển.

### ❓ Xử lý sự cố thường gặp (Troubleshooting)
- **Hỏi:** *Đã chạy Gateway nhưng `http://localhost:5000/health` không báo OK hoặc không truy cập được?*
  **Đáp:** Hãy kiểm tra xem Terminal 1 có báo lỗi báo đỏ nào không. Nếu có lỗi "Address in use", nghĩa là cổng 5000 đang bị chiếm (có thể do một Gateway cũ chưa tắt). Hãy nhấn `Ctrl + C` ở Terminal 1 vài lần để tắt hẳn, sau đó gõ `dotnet run` lại.
- **Hỏi:** *Ở Terminal 2 (Agent), mã PIN xuất hiện quá nhanh bị trôi mất thì làm sao lấy lại?*
  **Đáp:** Mã PIN sinh ra ngẫu nhiên mỗi lần Agent khởi động. Nếu bị trôi mất, bạn chỉ cần nhấn `Ctrl + C` để dừng Agent, sau đó gõ lại `dotnet run`. Agent sẽ kết nối lại và in ra một mã PIN 6 số mới toanh.
- **Hỏi:** *Ở Terminal 3, khi vào `localhost:5173` lại bị vào thẳng trang ghép cặp (không thấy chỗ đăng nhập), và danh sách Agent trống trơn?*
  **Đáp:** Điều này là do trình duyệt của bạn đã lưu phiên đăng nhập từ lần trước (cache), nhưng Database của Gateway hiện tại chưa có Agent nào. 
  -> **Cách xử lý:** Quay lại bước **Tạo Agent (Terminal 2)**, chạy 2 lệnh `curl` để tạo Agent. Sau khi tạo xong, quay lại trình duyệt và nhấn nút **"Làm mới danh sách"** (biểu tượng mũi tên xoay tròn), Agent sẽ hiện ra. Nếu bạn muốn đăng nhập lại từ đầu, hãy F12 -> Application -> Local Storage -> Xóa mục `auth_token` rồi F5 lại trang.

---

## 🖥 KỊCH BẢN 2: WINDOWS ĐIỀU KHIỂN MACBOOK
**Số lượng máy tính:** 2 máy.
- **Máy 1 (Windows)**: Vai trò **Operator**. Mở 2 terminal. Chạy Gateway và Web Client.
- **Máy 2 (MacBook)**: Vai trò **Target**. Mở 1 terminal. Chạy Agent.

### Yêu cầu cài đặt trước:
- Cả 2 máy phải kết nối **cùng một mạng WiFi/LAN**.
- Cả 2 máy đều có mã nguồn dự án.
- **Máy Windows**: Cài .NET 8 SDK, Node.js.
- **Máy MacBook**: Cài .NET 8 SDK.

### Chi tiết các bước thực hiện:

#### BƯỚC 1: TRÊN MÁY WINDOWS (OPERATOR)
1. **Tìm IP mạng LAN**:
   - Mở Command Prompt (cmd), gõ lệnh `ipconfig`. Tìm dòng *IPv4 Address* (VD: `192.168.1.15`). Ghi nhớ IP này.
2. **Mở Tường Lửa (Firewall)**:
   - Mở PowerShell dưới quyền **Administrator**. Chạy lệnh:
     ```powershell
     netsh advfirewall firewall add rule name="Allow Port 5000" dir=in action=allow protocol=TCP localport=5000
     ```
3. **Terminal 1 (Windows) - Chạy Gateway**:
   - Mở Terminal, trỏ tới thư mục Gateway. Chạy:
     ```bash
     dotnet run --urls "http://0.0.0.0:5000"
     ```
     *(Phải có `--urls` để mở kết nối cho mạng LAN)*
4. **Terminal 2 (Windows) - Chạy Web Client**:
   - Sửa file `src\WebClient\.env.local`:
     ```env
     VITE_GATEWAY_HTTP_URL=http://192.168.1.15:5000
     ```
     *(Thay `192.168.1.15` bằng IP thực tế của máy Win)*
   - Chạy lệnh:
     ```bash
     cd src\WebClient
     npm run dev -- --host
     ```
   - Mở trình duyệt tại `http://localhost:5173` (để tạo Agent mới chọn Platform "MacOS", lấy `AgentId` và `Secret` gửi sang Mac).

#### BƯỚC 2: TRÊN MÁY MACBOOK (TARGET)
1. **Terminal 1 (MacBook) - Chạy Agent**:
   - Sửa file `src/Agent/appsettings.json`:
     ```json
     "GatewayUrl": "ws://192.168.1.15:5000/ws",
     "AgentId": "<ID ĐƯỢC WINDOWS TẠO>",
     "AgentSecretKey": "<SECRET ĐƯỢC WINDOWS TẠO>"
     ```
   - Chạy Agent:
     ```bash
     cd src/Agent
     dotnet run
     ```
   - Đọc mã PIN cho máy Windows nhập vào trình duyệt để ghép cặp.

---

## 🍏 KỊCH BẢN 3: MACBOOK ĐIỀU KHIỂN WINDOWS
**Số lượng máy tính:** 2 máy.
- **Máy 1 (MacBook)**: Vai trò **Operator**. Mở 2 terminal. Chạy Gateway và Web Client.
- **Máy 2 (Windows)**: Vai trò **Target**. Mở 1 terminal. Chạy Agent.

### Yêu cầu cài đặt trước:
- Tương tự kịch bản 2. Cùng chung mạng WiFi.

### Chi tiết các bước thực hiện:

#### BƯỚC 1: TRÊN MÁY MACBOOK (OPERATOR)
1. **Tìm IP mạng LAN**:
   - Mở Terminal, gõ lệnh `ipconfig getifaddr en0`. (Giả sử ra IP: `192.168.1.20`).
2. **Terminal 1 (MacBook) - Chạy Gateway**:
   ```bash
   cd src/Gateway
   dotnet run --urls "http://0.0.0.0:5000"
   ```
3. **Terminal 2 (MacBook) - Chạy Web Client**:
   - Sửa file `src/WebClient/.env.local`:
     ```env
     VITE_GATEWAY_HTTP_URL=http://192.168.1.20:5000
     ```
   - Chạy lệnh:
     ```bash
     cd src/WebClient
     npm run dev -- --host
     ```
   - Mở trình duyệt, tạo Agent mới chọn Platform "Windows". Gửi `AgentId` và `Secret` sang máy Win.

#### BƯỚC 2: TRÊN MÁY WINDOWS (TARGET)
1. **Terminal 1 (Windows) - Chạy Agent**:
   - Sửa file `src\Agent\appsettings.json`:
     ```json
     "GatewayUrl": "ws://192.168.1.20:5000/ws",
     "AgentId": "<ID ĐƯỢC MAC TẠO>",
     "AgentSecretKey": "<SECRET ĐƯỢC MAC TẠO>"
     ```
   - Chạy Agent:
     ```bash
     cd src\Agent
     dotnet run
     ```
   - Lấy mã PIN đưa cho MacBook ghép cặp. 
   *(Lưu ý: Nếu Windows Defender báo chặn, hãy bấm "Allow access")*

---

## 🖥 KỊCH BẢN 4: WINDOWS ĐIỀU KHIỂN WINDOWS
**Số lượng máy tính:** 2 máy.
- **Máy 1 (Windows A)**: Vai trò **Operator**. Mở 2 terminal. (Chạy Gateway + Web Client).
- **Máy 2 (Windows B)**: Vai trò **Target**. Mở 1 terminal. (Chạy Agent).

### Yêu cầu cài đặt trước:
- Cả 2 máy chung mạng WiFi, đều cài .NET 8. Máy A cần cài thêm Node.js.

### Chi tiết các bước thực hiện:

#### BƯỚC 1: TRÊN MÁY WINDOWS A (OPERATOR)
1. **Mở Tường Lửa (Firewall)**:
   - Mở PowerShell quyền Admin và chạy lệnh cho phép Port 5000 như ở Kịch bản 2.
2. **Tìm IP mạng LAN**: Chạy `ipconfig` (Giả sử: `192.168.1.50`).
3. **Terminal 1 (Windows A) - Chạy Gateway**:
   ```bash
   cd src\Gateway
   dotnet run --urls "http://0.0.0.0:5000"
   ```
4. **Terminal 2 (Windows A) - Chạy Web Client**:
   - Sửa file `src\WebClient\.env.local` thành `VITE_GATEWAY_HTTP_URL=http://192.168.1.50:5000`
   - Chạy Web Client: `npm run dev -- --host`
   - Mở trình duyệt, tạo Agent cho Platform "Windows".

#### BƯỚC 2: TRÊN MÁY WINDOWS B (TARGET)
1. **Terminal 1 (Windows B) - Chạy Agent**:
   - Sửa `src\Agent\appsettings.json`:
     ```json
     "GatewayUrl": "ws://192.168.1.50:5000/ws"
     ```
   - Điền ID và Secret. Chạy `dotnet run`. Đưa mã PIN cho máy A.

---

## 📋 QUY TRÌNH TEST CÁC TÍNH NĂNG (Dùng cho cả 4 kịch bản)

Sau khi nhập PIN và ghép cặp thành công, bạn tiến hành test lần lượt trên Web Client như sau:

1. **Quản Lý Tiến Trình (Process)**
   - Click tab "Processes". Danh sách tiến trình máy Target phải tải lên.
   - Thử chức năng "Start Process": Gõ `notepad` (nếu target là Win) hoặc `calculator` (nếu target là Mac) -> Nhấn Start. App tương ứng phải tự bật lên.
   - Chọn app vừa mở trong danh sách -> Nhấn "Kill". App phải tự tắt đi.
2. **Theo Dõi Màn Hình (Screen View)**
   - Click tab "Screen". Trình duyệt phải hiện hình ảnh desktop của máy Target (cập nhật liên tục ~1-2 hình/giây).
   - *(Lưu ý máy Mac: Máy Target phải cấp quyền Screen Recording trong System Settings).*
3. **Webcam**
   - Click tab "Webcam". Camera máy Target phải bật (đèn sáng) và truyền video lên trình duyệt.
   - *(Lưu ý máy Mac: Máy Target phải cấp quyền Camera).*
4. **Quản Lý Tập Tin (File Browser)**
   - Click tab "Files". Danh sách đĩa / thư mục phải hiện ra.
   - Truy cập thử `C:\` hoặc `/Users/`. Bạn phải thấy được cấu trúc file.
5. **Keylogger**
   - Click nút "Enable Keylogger".
   - Quay sang máy Target, gõ văn bản bất kỳ (có thể mở Notepad/Word để gõ).
   - Trở lại Web Client, bạn phải thấy từng phím được gõ hiển thị real-time.
   - *(Lưu ý máy Mac: Cần cấp quyền Accessibility. Máy Win: Có thể bị Windows Defender diệt, cần add Exception).*
