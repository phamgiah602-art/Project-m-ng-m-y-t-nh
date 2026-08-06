# Bộ Câu Hỏi Vấn Đáp Luyện Tập

Khi ra hội đồng bảo vệ, giáo viên có thể chỉ vào một file hoặc hỏi một tính năng. Đây là các câu hỏi thường gặp, chia theo 3 mức độ, kèm theo đáp án dựa sát vào source code thực tế của đồ án.

## Mục lục
1. [Mức độ Cơ bản](#1-muc-do-co-ban)
2. [Mức độ Trung bình](#2-muc-do-trung-binh)
3. [Mức độ Nâng cao](#3-muc-do-nang-cao)

---

## 1. Mức độ Cơ bản

**Câu 1: File `Program.cs` ở project Gateway có nhiệm vụ gì?**
> **Trả lời:** Là điểm khởi đầu (entry point) của máy chủ Gateway. Nó dùng để cấu hình kết nối database SQLite, thiết lập cơ chế xác thực JWT Token, cấu hình đường dẫn API, và đặc biệt là mở điểm kết nối `app.Map("/ws", ...)` để lắng nghe các kết nối WebSocket đến.

**Câu 2: Tại sao chúng ta cần một máy chủ Gateway ở giữa thay vì để Client (web) kết nối thẳng vào Agent?**
> **Trả lời:** Do 2 lý do chính: 
> 1. Máy Agent thường nằm trong mạng LAN/NAT, không có IP công cộng để Client gọi thẳng vào. Gateway đóng vai trò "trung tâm" có IP công cộng để cả hai bên cùng kết nối lên.
> 2. Gateway làm nhiệm vụ xác thực (kiểm tra token/mã PIN) đảm bảo an toàn, không ai có thể tự do truy cập Agent.

**Câu 3: WebSocket khác gì với REST API (HTTP)? Dự án này dùng cái nào?**
> **Trả lời:** REST API là giao tiếp 1 chiều (Client gọi, Server trả lời rồi ngắt kết nối). WebSocket là kết nối 2 chiều liên tục, gửi nhận dữ liệu thời gian thực mà không cần gọi lại từ đầu.
> Dự án dùng **cả hai**: REST API dùng để Đăng nhập (lấy JWT Token); WebSocket dùng cho toàn bộ quá trình điều khiển, xem màn hình, vì cần độ trễ thấp và duy trì kết nối liên tục.

**Câu 4: Khi muốn vẽ giao diện web, nhóm sử dụng thư viện/framework gì? Chức năng của nó?**
> **Trả lời:** Nhóm sử dụng thư viện **React** kết hợp với **Vite** để chạy server code. React giúp chia nhỏ giao diện thành các Component (tái sử dụng). Để làm đẹp, nhóm dùng **TailwindCSS** (CSS utility) để gán trực tiếp thuộc tính CSS vào thẻ HTML mà không cần viết file css dài dòng.

**Câu 5: Database trong Gateway được quản lý bằng công nghệ gì? Loại Database là gì?**
> **Trả lời:** Nhóm sử dụng công nghệ **Entity Framework Core** (EF Core) để tương tác với cơ sở dữ liệu, không cần viết lệnh SQL thuần. Database thực tế sử dụng là **SQLite** (file `remotecontrol.db`), vì nó gọn nhẹ và lưu thẳng dưới dạng 1 file trong thư mục dự án.

---

## 2. Mức độ Trung bình

**Câu 6: File `MessageRouter.cs` trong Gateway xử lý dữ liệu như thế nào?**
> **Trả lời:** Nó nhận một chuỗi JSON gửi từ WebSocket, đọc trường `Action` để biết đó là lệnh gì. Tiếp theo nó chia làm 2 tập: `ToAgent` (ví dụ: `START_PROCESS`) và `ToBrowser` (ví dụ: `SCREEN_FRAME`). Dựa vào `SessionId`, nó tìm xem đối tác (Client hoặc Agent) của phiên đó đang có `connectionId` là gì, rồi "chuyển tiếp" (forward) nguyên vẹn chuỗi JSON đó sang đối tác.

**Câu 7: Nếu xóa mất file `wsClient.ts` bên Client thì điều gì sẽ xảy ra?**
> **Trả lời:** Toàn bộ chức năng điều khiển từ xa sẽ bị tê liệt. File `wsClient.ts` là trái tim của giao tiếp thời gian thực bên Client, nó định nghĩa class quản lý đối tượng `WebSocket` của trình duyệt, đóng gói lệnh thành JSON (`send()`) và lắng nghe phản hồi (`onmessage`). Không có nó, React chỉ hiển thị được giao diện tĩnh.

**Câu 8: Cấu trúc `async` / `await` và `Task` trong C# (Agent và Gateway) dùng để giải quyết vấn đề gì?**
> **Trả lời:** Dùng để xử lý bất đồng bộ (Asynchronous). Nếu không có `await`, khi Gateway đọc/ghi database hoặc đọc dữ liệu WebSocket (tốn thời gian), luồng thực thi (thread) sẽ bị block (treo). Có `await`, thread đó sẽ được giải phóng để đi phục vụ người khác, khi có kết quả mới quay lại xử lý tiếp. Giúp máy chủ chịu tải cao hơn mà không bị đơ.

**Câu 9: Agent phân biệt hệ điều hành Windows và macOS bằng cách nào?**
> **Trả lời:** Trong Agent, nhóm sử dụng kỹ thuật Factory Pattern, file `PlatformServiceFactory.cs`. Tùy thuộc vào hàm `OperatingSystem.IsMacOS()` hay `IsWindows()`, nó sẽ trả về các công cụ lấy tiến trình (Process) hoặc chụp màn hình (Screen) tương ứng của hệ điều hành đó.

**Câu 10: Làm sao Gateway biết tin nhắn WebSocket gửi lên thuộc về ai (đã đăng nhập hay chưa)?**
> **Trả lời:** Khi khởi tạo WebSocket ở Client, token được nhét vào tham số protocol: `new WebSocket(url, ['bearer', token])`. Lên tới Gateway (`Program.cs`), middleware của ASP.NET sẽ tách chuỗi "bearer" ra để lấy token, giải mã (verify JWT), nếu hợp lệ mới sinh ra thông tin `User` gắn vào HTTP Context, lúc đó `WebSocketEndpoint` mới chấp nhận kết nối.

---

## 3. Mức độ Nâng cao

**Câu 11: Nếu Gateway sập (crash) đột ngột, Agent làm sao để nhận biết và xử lý?**
> **Trả lời:** Ở file `GatewayConnection.cs` của Agent, quá trình kết nối được đặt trong một vòng lặp `while` kết hợp với khối `try/catch`. Nếu Gateway sập, hàm `socket.ReceiveAsync` sẽ văng Exception. Lập tức catch nhảy vào, chờ một khoảng thời gian (cấp số nhân - exponential backoff, `retry * 2`), rồi lặp lại để tạo kết nối mới liên tục cho đến khi Gateway sống lại.

**Câu 12: Hãy mô tả lại luồng bảo mật (Security Flow) khi Client muốn ghép cặp (Pair) với một Agent?**
> **Trả lời:** 
> 1. Client login REST API -> có JWT Token.
> 2. Client mở WebSocket, mang Token theo -> Gateway xác nhận "người thật".
> 3. Client gửi `REQUEST_PAIRING` kèm ID của Agent và mã PIN qua WebSockets.
> 4. Gateway vào Database đối chiếu PIN. Nếu đúng, Gateway tạo một bản ghi `Session` (phiên), lưu kết nối của Client và kết nối của Agent vào 1 cặp (`SessionBinding`). 
> 5. Từ lúc này, mọi tin nhắn qua lại đều phải có `SessionId` chuẩn thì Gateway mới cho Forward.

**Câu 13: Dependency Injection (DI) trong dự án này đóng vai trò gì? Hãy chỉ ra một ví dụ.**
> **Trả lời:** DI giúp các class không cần tự khởi tạo phụ thuộc bằng từ khóa `new`, giảm sự kết dính (loose coupling) và dễ test. Ví dụ: Trong `MessageRouter.cs`, thay vì ghi `var db = new AppDbContext()`, class này chỉ khai báo `IServiceScopeFactory scopeFactory` ở hàm tạo. Hệ thống sẽ tự động bơm (inject) Factory này vào, giúp Router gọi các Service khác một cách an toàn.

**Câu 14: Tại sao trong Gateway, tin nhắn nhận từ WebSocket không được chuyển thẳng mà lại phải Deserialize thành `MessageEnvelope`?**
> **Trả lời:** Để Gateway hiểu được tin nhắn đó đang làm gì. `MessageEnvelope` chứa các trường cơ bản như `Action` (lệnh gì), `SessionId` (phiên nào). Gateway chỉ cần đọc phần vỏ (envelope) để biết cần chuyển cho ai (dựa vào Action thuộc `ToAgent` hay `ToBrowser`) và kiểm tra quyền, mà KHÔNG cần quan tâm ruột chi tiết (payload) bên trong là hình ảnh hay text.

**Câu 15: Tại sao dự án chọn dùng Raw WebSocket thay vì SignalR, dù SignalR có sẵn và dễ dùng hơn trong ASP.NET Core?**
> **Trả lời:** (Lưu ý: tuỳ thuộc vào quyết định thực tế của nhóm, nhưng lý do kỹ thuật chuẩn nhất là): Dùng Raw WebSocket giúp tối ưu kích thước gói tin và hiệu năng khi truyền dữ liệu binary lớn hoặc truyền màn hình liên tục, tránh overhead (thông tin thừa) của giao thức SignalR. Đồng thời giúp code C# Agent nhẹ hơn, không cần phải kéo các thư viện client phức tạp của SignalR về.
