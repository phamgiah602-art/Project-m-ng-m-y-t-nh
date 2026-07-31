# Hướng dẫn chạy demo

## 1. Điều kiện

- Cài .NET SDK 8.0 trên máy chạy Gateway và Agent.
- Cài Node.js 20+ trên máy chạy Web Client.
- Gateway và Agent nằm trong cùng LAN. Mở TCP port Gateway (mặc định `5000`) trong firewall nếu dùng máy khác.

## 2. Chạy Gateway

```bash
cd src/Gateway
dotnet restore
dotnet run --urls http://0.0.0.0:5000
```

Lần chạy đầu tự tạo SQLite `remotecontrol.db`. Trước khi demo, đổi `Jwt:Key` qua secret ngẫu nhiên dài hơn 32 ký tự trong `appsettings.Local.json` (file này đã bị Git ignore).

## 3. Tạo Operator và Agent credential

1. Mở Web Client, đăng ký Operator.
2. Gọi `POST /api/agents` với JWT Operator, body ví dụ:

```json
{ "agentName": "Lab-PC-01", "platform": "MacOS" }
```

3. Lưu **một lần duy nhất** `agentId` và `agentSecretKey` mà endpoint trả về. Thêm hai giá trị này, cùng `GatewayUrl`, vào `src/Agent/appsettings.Local.json`.

## 4. Chạy Agent và Web Client

```bash
cd src/Agent
dotnet run

cd ../WebClient
npm install
npm run dev
```

Agent sẽ thông báo mã PIN 6 số. Operator mở Web Client, chọn Agent, nhập PIN rồi thực hiện các chức năng được hiển thị.

## 5. Quyền hệ điều hành

- macOS: cấp **Screen Recording** cho screen view, **Camera** cho webcam, và **Accessibility** cho keyboard hook đã được phê duyệt.
- Windows: chạy Agent trong phiên desktop có quyền chụp màn hình/camera.
- Shutdown/Restart luôn yêu cầu nhập lại mật khẩu Operator và mặc định bị chặn tại Agent cho đến khi bật `AllowPowerCommands`.

Không triển khai Gateway ra Internet; thiết kế `ws://` này chỉ dùng cho LAN demo kín.
