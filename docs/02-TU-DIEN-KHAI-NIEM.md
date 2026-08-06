# Từ Điển Khái Niệm - Dành Cho Người Mới Bắt Đầu

Trong code của dự án, bạn sẽ thấy hàng loạt từ khóa tiếng Anh viết tắt. Đừng sợ, phần này sẽ giải thích chúng giống như bạn đang nghe kể chuyện đời thường.

## Mục lục
1. [Khái niệm tổng quát](#1-khai-niem-tong-quat)
2. [Khái niệm bên Gateway (ASP.NET Core / C#)](#2-khai-niem-ben-gateway-aspnet-core--c)
3. [Khái niệm bên Client (React / Web)](#3-khai-niem-ben-client-react--web)

---

## 1. Khái niệm tổng quát

- **Framework là gì? Tại sao dùng ASP.NET Core hay React thay vì code thuần?**
  *Giải thích*: Tưởng tượng bạn muốn xây một căn nhà. "Code thuần" giống như bạn phải tự đi lấy bùn nặn thành gạch, tự đốn cây làm cột. "Framework" giống như bạn ra cửa hàng mua gạch làm sẵn, cột đúc sẵn về lắp ráp lại. React và ASP.NET Core là các cửa hàng bán "vật liệu xây dựng phần mềm" sẵn có, giúp bạn code nhanh, an toàn và ít lỗi hơn.

- **HTTP REST API**
  *Giải thích*: Nó giống như việc gọi món ở nhà hàng bằng tờ giấy (menu). Bạn tích vào tờ giấy yêu cầu "Cho 1 tô phở" (đây là **Request**), người phục vụ mang vào bếp, sau đó mang ra cho bạn 1 tô phở (đây là **Response**). Đặc điểm là xong việc là đường ai nấy đi, bạn muốn gọi thêm trà đá thì phải lấy tờ giấy khác điền lại từ đầu.

- **WebSocket (RAW WebSocket)**
  *Giải thích*: Khác với REST API (gọi món bằng giấy), WebSocket giống như một đường dây điện thoại nối trực tiếp từ bàn bạn vào tai đầu bếp. Bạn nói "Cho thêm chanh", đầu bếp nghe ngay lập tức và đưa ra. Kết nối này duy trì liên tục cho đến khi bạn cúp máy. Nó rất cần thiết để xem màn hình từ xa mượt mà (vì phải gửi liên tục hàng chục ảnh mỗi giây).

- **JSON (JavaScript Object Notation)**
  *Giải thích*: Là cách để các hệ thống khác ngôn ngữ (C# và JavaScript) hiểu nhau. Thay vì gửi một cục dữ liệu lộn xộn, người ta gói nó vào một format chuẩn theo cặp "Chìa khóa : Giá trị". 
  Ví dụ: `{"Tên": "Nam", "Tuổi": 20}`.

---

## 2. Khái niệm bên Gateway (ASP.NET Core / C#)

- **Dependency Injection (DI)**
  *Giải thích*: Giả sử bạn là một Thợ mộc, để làm việc bạn cần Búa và Cưa. Thay vì bạn tự phải đi mua Búa (tự tạo object búa trong class), thì sẽ có một "Quản lý" (DI Container) tự động dí cái Búa vào tay bạn khi bạn bắt đầu làm việc. Trong `Program.cs`, các dòng `builder.Services.AddScoped...` chính là việc nhập kho các dụng cụ để "Quản lý" tự phát cho các file khi cần.

- **Controller / Endpoint**
  *Giải thích*: Nó giống như các "cửa quầy giao dịch" trong ngân hàng. Khi bạn truy cập vào đường link `http://localhost/api/login`, bạn đang đi đến quầy giao dịch tên là "Login" (tức là LoginController) để nhờ nhân viên ở quầy đó xử lý.

- **async / await và Task (C#)**
  *Giải thích*: Khi Gateway nhận lệnh "Xóa file này trên ổ cứng", việc xóa tốn thời gian. Nếu không dùng `async/await`, chương trình sẽ "đứng hình" chờ xóa xong mới làm việc khác (chặn luồng - blocking). Dùng `async/await`, chương trình bảo: "Bắt đầu xóa đi nhé (Task), trong lúc chờ mày xóa tao đi phục vụ người khác, khi nào xóa xong tao quay lại báo kết quả (await)". Giúp hệ thống không bị đơ.

- **Entity Framework Core (EF Core)**
  *Giải thích*: Bạn muốn lưu thông tin vào Database nhưng không biết viết câu lệnh SQL lằng nhằng (`SELECT * FROM Users`)? EF Core là một người phiên dịch. Bạn chỉ cần viết code C# như `dbContext.Users.Add(user)`, người phiên dịch sẽ tự động đổi nó thành câu lệnh SQL để đưa vào Database.

- **JWT (JSON Web Token)**
  *Giải thích*: Sau khi bạn đăng nhập thành công, hệ thống không thể lúc nào cũng hỏi "Bạn là ai?" ở mỗi thao tác. Nó sẽ cấp cho bạn một cái "Thẻ ra vào" (chính là JWT). Mỗi lần bạn gửi yêu cầu lên, bạn kẹp thẻ này vào. Hệ thống chỉ cần soi thẻ xem còn hạn không, do ai cấp là cho qua.

---

## 3. Khái niệm bên Client (React / Web)

- **Component**
  *Giải thích*: Trong React, người ta không xây toàn bộ trang web trong 1 file. Họ chia nhỏ ra: một nút bấm là 1 component, cái thanh menu trên cùng là 1 component. Component giống như những "miếng logo" độc lập, làm xong 1 miếng bạn có thể tái sử dụng dán nó ở mọi nơi. 

- **Props (Properties)**
  *Giải thích*: Khi bạn có 1 Component "Nút Bấm", làm sao để ở trang A nút đó màu Đỏ, trang B nút màu Xanh? Bạn sẽ truyền `Props` cho nó. `Props` giống như các thông số cấu hình bạn đưa cho thợ may (tôi muốn áo màu đỏ, size M) để họ may ra cái áo tương ứng.

- **State**
  *Giải thích*: State là trí nhớ của Component. Khi bạn bấm nút "Phóng to", Component cần ghi nhớ trạng thái "Đang được phóng to" vào bộ nhớ State của mình để màn hình tự vẽ lại kích thước lớn hơn. Khi State thay đổi, React sẽ tự động vẽ (render) lại phần giao diện đó.

- **Hook (ví dụ: `useState`, `useEffect`)**
  *Giải thích*: Các "móc câu" giúp Component "móc" vào các tính năng sâu của React. `useState` giúp Component có trí nhớ (State). `useEffect` giúp Component biết phải làm gì khi vừa mới xuất hiện trên màn hình (ví dụ: vừa mở màn hình thì tự động gọi API lấy danh sách máy tính).

- **TailwindCSS**
  *Giải thích*: Thay vì viết riêng một file CSS mệt mỏi (`.nut-bam { color: red; padding: 10px; }`), Tailwind cho phép bạn gõ thẳng các từ khóa viết tắt vào thuộc tính class của HTML (`class="text-red-500 p-2"`). Nó giúp làm đẹp web siêu nhanh.
