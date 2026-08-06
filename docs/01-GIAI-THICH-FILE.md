# Giải thích Cấu trúc & Từng File trong Dự án

Tài liệu này giải phẫu từng thư mục và file quan trọng trong dự án, giúp bạn biết đoạn code nào đang làm nhiệm vụ gì và không bị bỡ ngỡ khi bị giáo viên hỏi "File này viết ra để làm gì?".

## Mục lục
1. [Gateway (Máy chủ ASP.NET Core)](#1-gateway)
2. [Agent (Phần mềm máy khách C#)](#2-agent)
3. [Client (Giao diện React Web)](#3-client)

---

## 1. Gateway (ASP.NET Core)

Thư mục: `src/Gateway/`
**Mục đích**: Là tổng đài trung tâm, nhận kết nối từ cả Client và Agent, quản lý đăng nhập, ghi log, và định tuyến (chuyển tiếp) tin nhắn qua lại.

### `Program.cs`
- **Mục đích**: Đây là "cửa ngõ" của Gateway, nơi hệ thống bắt đầu chạy. Nó thiết lập toàn bộ cấu hình, kết nối Database, và khởi tạo các Middleware (phần sụn ở giữa).
- **Các thành phần chính**:
  - `builder.Services.AddDbContext<AppDbContext>(...)`: Báo cho hệ thống biết chúng ta dùng SQLite để lưu dữ liệu.
  - `builder.Services.AddAuthentication(...)`: Cài đặt cơ chế kiểm tra vé (JWT Token) để biết user đã đăng nhập chưa.
  - `app.UseWebSockets()` và `app.Map("/ws", ...)`: Bật tính năng WebSocket và mở một đường hầm tên là `/ws` để đón kết nối tới.
- **Nó gọi ai**: Nó gọi tất cả các Repository và Service (bằng cách nhúng nó vào "Thùng chứa" Dependency Injection).
- **Đoạn code khó hiểu với người mới**:
  ```csharp
  app.Map("/ws", context => context.RequestServices.GetRequiredService<WebSocketEndpoint>().HandleAsync(context));
  ```
  *Giải thích*: Dòng này có nghĩa là "Nếu có ai đó truy cập vào đường dẫn `http://localhost:xxx/ws`, hãy lấy công cụ có tên là `WebSocketEndpoint` ra và ném toàn bộ yêu cầu (context) cho nó xử lý".

### `WebSockets/WebSocketEndpoint.cs`
- **Mục đích**: Quản lý vòng đời (sống/chết) của một kết nối WebSocket.
- **Các thành phần chính**: Hàm `HandleAsync`. Khi có kết nối mới, nó tạo một ID ngẫu nhiên, lưu vào danh sách quản lý. Sau đó dùng một vòng lặp `while` chạy liên tục để "hứng" tin nhắn gửi lên.
- **Nó gọi ai**: Khi hứng được tin nhắn chữ (JSON), nó gọi `router.RouteAsync(...)` để xử lý tiếp.

### `WebSockets/MessageRouter.cs`
- **Mục đích**: Trạm phân loại bưu kiện. Đọc tin nhắn JSON, xem nó là lệnh gì (`Action`), và quyết định sẽ chuyển nó đi đâu.
- **Các thành phần chính**:
  - Hai danh sách `ToAgent` và `ToBrowser`: Quy định loại tin nhắn nào thì được phép gửi cho Agent (ví dụ: `START_PROCESS`), loại nào gửi cho Client (ví dụ: `PROCESS_LIST_RESULT`).
  - Hàm `RouteAsync`: Xử lý lõi.
- **Đoạn code khó hiểu với người mới**:
  ```csharp
  var toAgent = ToAgent.Contains(message.Action); 
  var toBrowser = ToBrowser.Contains(message.Action);
  ```
  *Giải thích*: Kiểm tra xem cái tên hành động (Action) trong tin nhắn có nằm trong danh sách được phép chuyển đến Agent hay Browser không. Nếu không có trong cả hai, sẽ báo lỗi.

---

## 2. Agent (C# Worker Service)

Thư mục: `src/Agent/`
**Mục đích**: Chạy ngầm trên máy tính bị điều khiển, duy trì kết nối với Gateway để nhận lệnh, và tương tác sâu với hệ điều hành (chụp màn hình, lấy tiến trình).

### `Program.cs`
- **Mục đích**: Nơi khởi động Agent. Khác với Gateway là ứng dụng Web, Agent là ứng dụng kiểu "Worker" (chỉ chạy ngầm không có giao diện web).
- **Các thành phần chính**: 
  - `PlatformServiceFactory`: Kiểm tra xem máy đang chạy là Windows hay macOS để cung cấp công cụ tương ứng (vì lệnh lấy tiến trình ở Windows khác Mac).
- **Đoạn code khó hiểu với người mới**:
  ```csharp
  builder.Services.AddHostedService<AgentWorker>();
  ```
  *Giải thích*: Ra lệnh cho chương trình bắt đầu chạy class `AgentWorker` như một tiến trình ngầm (dịch vụ chạy nền liên tục) ngay khi app khởi động.

### `Services/GatewayConnection.cs`
- **Mục đích**: Đảm nhận việc kết nối, gửi và nhận dữ liệu lên Gateway qua WebSocket.
- **Các thành phần chính**: 
  - Hàm `RunAsync`: Chứa vòng lặp để tự động kết nối lại (reconnect) mỗi khi rớt mạng.
  - Hàm `SendAsync`: Gói cục dữ liệu thành JSON rồi ném qua mạng.
  - `ReceiveLoopAsync`: Vòng lặp đợi nghe ngóng tin nhắn từ Gateway gửi về.
- **Đoạn code khó hiểu với người mới**:
  ```csharp
  var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message, JsonConfig.Default));
  await socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
  ```
  *Giải thích*: Dữ liệu trên mạng chỉ truyền đi bằng "byte" (số 0 và 1). Dòng 1: Biến biến (message) thành chuỗi chữ JSON, sau đó ép chuỗi chữ đó thành một mảng các byte. Dòng 2: Bắn mảng byte đó qua ống WebSocket.

---

## 3. Client (React & Vite)

Thư mục: `src/WebClient/src/`
**Mục đích**: Là giao diện hiển thị trên trình duyệt. Người dùng sẽ click chuột trên này để gửi lệnh và xem màn hình của máy tính từ xa.

### `main.tsx` và `App.tsx`
- **Mục đích**: `main.tsx` là điểm bắt đầu cắm React vào file HTML. `App.tsx` là component mẹ, chứa bố cục (layout) chính của toàn bộ website và hệ thống điều hướng (Router).

### `services/wsClient.ts`
- **Mục đích**: Tương tự như `GatewayConnection.cs` của Agent, đây là công cụ phía React dùng để tạo ống nước WebSocket tới Gateway.
- **Các thành phần chính**: 
  - `connect(token)`: Khởi tạo đối tượng `new WebSocket()` của trình duyệt.
  - `send(action, payload)`: Gửi lệnh JSON đi.
- **Đoạn code khó hiểu với người mới**:
  ```typescript
  this.socket.onmessage = event => { 
      const message = JSON.parse(event.data) as Envelope;
      this.listeners.forEach(listener => listener(message));
  };
  ```
  *Giải thích*: `onmessage` là sự kiện tự động kích hoạt khi có tin báo về từ mạng. Dòng 1: Nó dịch cục chữ JSON thành Object của Javascript. Dòng 2: Nó báo cho tất cả các giao diện nào đang "nghe ngóng" (listener) biết rằng có tin mới để họ tự update màn hình.
