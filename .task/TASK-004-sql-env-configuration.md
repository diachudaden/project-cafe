# TASK-004: Cấu hình thông tin SQL local qua file `.env` được Git bỏ qua

- Trạng thái: Bị chặn
- Mục tiêu: Cấu hình API thực tế sử dụng kết nối SQL của lập trình viên từ file `.env` local, không để lộ thông tin xác thực trong Git hoặc log.
- Trở ngại: Hiện repository chưa có API `.csproj`, `Program.cs`, cấu hình database, `.gitignore` hay cấu hình runtime cho thấy ứng dụng sẽ nạp file `.env` như thế nào. Chưa xác định SQL engine và môi trường chạy/deploy. Không tự đoán các thông tin này hoặc thêm credential trước khi ứng dụng có thể sử dụng chúng.

## Tài liệu đặc tả và quy tắc

- [../spec.md](../spec.md)
- [../docs/payments/SPEC.md](../docs/payments/SPEC.md)
- [../.rule/security.md](../.rule/security.md)
- [../.rule/architecture.md](../.rule/architecture.md)
- [../README.md](../README.md)

## Phụ thuộc

- [TASK-002](./backlog.md): tích hợp module thanh toán với domain và tầng persistence thực tế của ứng dụng, bao gồm API và cấu hình database thực.

## Phạm vi

- Xác định database engine/provider thực tế, API startup project, khóa connection string và cách chạy/deploy local.
- Tạo file `.env` local cho credential lập trình viên và đảm bảo Git bỏ qua file này.
- Cung cấp file `.env.example` được commit, chỉ chứa tên biến và giá trị giả an toàn.
- Kết nối quy trình runtime/development hiện có để nạp biến môi trường vào cấu hình database của ứng dụng.
- Hướng dẫn thiết lập, kiểm tra và thay mới credential.

## Các bước thực hiện

1. **Xác nhận ứng dụng và database đích**
   - Tìm API project có thể chạy, cấu hình khởi động, đăng ký DbContext/database và thiết lập migration.
   - Xác nhận SQL engine/provider, các trường connection string cần thiết, phương thức xác thực và runtime local (ví dụ Docker Compose hoặc `dotnet run`) với chủ dự án.
   - Không sao chép credential vào chat, task, mã nguồn, log hoặc lịch sử lệnh.

2. **Chọn và kiểm tra cách nạp `.env`**
   - Kiểm tra runtime local hiện tại có đọc `.env` sẵn không (ví dụ Docker Compose `env_file`).
   - Lưu ý .NET không tự nạp file `.env`. Nếu chạy API trực tiếp bằng `dotnet run`, hãy dùng loader đã được dự án phê duyệt hoặc điều chỉnh quy trình chạy local để xuất biến môi trường; không thêm dependency mới khi chưa xác nhận là cần thiết và được duyệt.
   - Chỉ dùng dạng biến môi trường .NET `ConnectionStrings__DefaultConnection` nếu ứng dụng được cấu hình đọc `ConnectionStrings:DefaultConnection`; nếu không, dùng khóa connection string mà ứng dụng thực tế yêu cầu.

3. **Bảo vệ credential local**
   - Thêm quy tắc `.gitignore` phù hợp để bỏ qua `.env` và các file môi trường local khác, đồng thời cho phép theo dõi `.env.example`.
   - Tạo `.env.example` với các tên biến đã xác nhận và giá trị placeholder; tuyệt đối không đưa hostname, username, password hoặc token thật vào file.
   - Tạo `.env` local và nhập credential SQL do lập trình viên cung cấp. Không thêm file đã điền credential vào Git.

4. **Kết nối cấu hình với database**
   - Đưa biến môi trường đã xác nhận vào đăng ký database provider hiện có, không thay đổi hành vi database ngoài phạm vi task.
   - Giữ nguyên các tùy chọn connection và yêu cầu mã hóa/TLS theo provider; không tắt xác thực chứng chỉ để làm cho kết nối chạy được.
   - Khi thiếu cấu hình, thông báo lỗi phải nêu rõ thiết lập nào bị thiếu nhưng không in giá trị của nó.

5. **Kiểm tra local và cập nhật hướng dẫn**
   - Xác nhận Git bỏ qua `.env` và theo dõi `.env.example`.
   - Chạy API theo quy trình local đã ghi trong tài liệu và xác nhận kết nối tới đúng database.
   - Chạy migration phù hợp hoặc kiểm tra kết nối database an toàn; không áp dụng thay đổi schema có khả năng phá hủy lên database không rõ nguồn gốc hoặc môi trường production.
   - Xác nhận log và thông báo lỗi không chứa connection string hoặc password.
   - Ghi lại chính xác lệnh thiết lập/chạy local và cách thay mới hoặc xóa credential.

## Tiêu chí hoàn thành

- API dùng SQL provider đã cấu hình và đọc connection string từ biến môi trường dự kiến.
- Lập trình viên có thể tạo và sử dụng `.env` local theo quy trình runtime được hướng dẫn.
- Git bỏ qua `.env`; `.env.example` được commit và chỉ chứa placeholder.
- Credential không xuất hiện trong file được theo dõi, đầu ra lệnh hoặc log ứng dụng.
- API kết nối thành công tới database không phải production đã xác nhận; ghi lại lệnh kiểm tra tập trung tương ứng.
- Tài liệu nêu rõ tùy chọn theo provider và cách quản lý secret khi deploy, không mặc định `.env` local phù hợp cho production.

## Bằng chứng kiểm tra

- Chờ ghi lại chính xác các lệnh và kết quả sau khi có API có thể chạy và SQL provider.
