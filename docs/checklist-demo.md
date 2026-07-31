# Checklist demo LAN

## Trước khi chạy

- [ ] Đổi `Jwt:Key` bằng secret ngẫu nhiên ít nhất 32 ký tự.
- [ ] Cài .NET SDK 8 trên Gateway và Target; cài Node 20+ trên máy Browser.
- [ ] Gateway chạy `dotnet ef database update` (hoặc `dotnet run`, app tự apply migration).
- [ ] Tạo Agent credential qua `POST /api/agents`; không commit AgentSecretKey.
- [ ] Cập nhật `GatewayUrl`, AgentId và AgentSecretKey trên máy Target.
- [ ] Kiểm tra các máy cùng LAN và Browser gọi được `http://<gateway-ip>:5000/health`.
- [ ] Mở firewall TCP 5000 trên Gateway khi cần.

## Quyền và hiển thị Target

- [ ] Agent hiển thị notification PIN khi khởi động.
- [ ] macOS: Screen Recording đã cấp cho Agent.
- [ ] macOS: Camera đã cấp cho Agent trước khi demo webcam.
- [ ] macOS: Accessibility chỉ cấp khi demo keyboard hook có người Target đồng ý.
- [ ] Agent hiển thị thông báo khi phiên điều khiển, webcam hoặc keyboard logging được yêu cầu.
- [ ] Không bật `AllowPowerCommands` trừ thiết bị được phép tắt/khởi động lại.

## Luồng demo bắt buộc

- [ ] Đăng ký Operator, đăng nhập, chọn Agent và pairing bằng PIN 6 số.
- [ ] List process, thử stop một process không phải hệ thống và xác nhận process hệ thống bị chặn.
- [ ] Stream màn hình ở Medium (400ms); thử tắt/bật stream.
- [ ] Bật webcam và kiểm tra UI/OS permission.
- [ ] Duyệt thư mục được phép, upload/download một file nhỏ và xác nhận checksum.
- [ ] Thử đường dẫn cấm để xác nhận `PATH_BLOCKED`.
- [ ] Bấm bật keyboard logging, xác nhận Target từ chối trước rồi đồng ý trong lần thứ hai.
- [ ] Thử shutdown/restart chỉ sau reverify password; giữ `AllowPowerCommands=false` nếu không demo nguồn.
- [ ] Tắt Wi-Fi Agent 45 giây, kiểm tra Browser nhận `AGENT_DISCONNECTED`, bật lại để kiểm tra reconnect.

## Sau demo

- [ ] Xem `GET /api/audit` để minh họa audit log.
- [ ] Thu hồi hoặc thay AgentSecretKey nếu credential đã lộ trong lúc demo.
- [ ] Xóa database demo nếu chứa dữ liệu không cần giữ.
