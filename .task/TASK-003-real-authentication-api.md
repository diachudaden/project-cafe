# TASK-003: Triển khai API xác thực thật

- Trạng thái: Hoàn thành
- Mục tiêu: Cung cấp API xác thực backend thật và hợp đồng OpenAPI được tài liệu hóa cho các ứng dụng frontend.
- Lý do: Luồng đăng nhập phải gọi backend; dữ liệu nghiệp vụ giả không áp dụng cho xác thực.

## Tài liệu đặc tả

- [../spec.md](../spec.md)
- [../README.md](../README.md)

## Tài liệu quy tắc

- [../.rule/architecture.md](../.rule/architecture.md)
- [../.rule/security.md](../.rule/security.md)
- [../.rule/testing.md](../.rule/testing.md)
- [../.github/instructions/backend.instructions.md](../.github/instructions/backend.instructions.md)
- [../.github/instructions/tests.instructions.md](../.github/instructions/tests.instructions.md)

## Phụ thuộc

- [TASK-002](./TASK-002-frontend-auth-requirements.md)

## Phạm vi

- Triển khai hành vi xác thực đã được duyệt theo ranh giới các layer backend hiện có.
- Chỉ thêm endpoint đăng nhập, lấy thông tin người dùng hiện tại, đăng xuất và gia hạn phiên nếu TASK-002 yêu cầu.
- Công bố chi tiết request, response, lỗi và cơ chế bảo mật trong OpenAPI.
- Cấu hình CORS và xử lý thông tin xác thực phù hợp với các origin/môi trường của ba ứng dụng frontend.
- Bổ sung test backend tập trung cho đăng nhập thành công, thông tin đăng nhập không hợp lệ và các trường hợp lỗi/phiên khác đã được duyệt.

## Ngoài phạm vi

- Không tự chọn nhà cung cấp xác thực, chính sách thông tin đăng nhập, mô hình vai trò hoặc cách dùng token/cookie.
- Không trả về phản hồi thành công giả hoặc dùng thông tin đăng nhập hard-code thay cho xác thực thật.
- Không thêm endpoint nghiệp vụ không liên quan đến đăng nhập.

## Tiêu chí chấp nhận

- Backend triển khai đúng hợp đồng xác thực đã được duyệt trong TASK-002.
- Lỗi xác thực trả về phản hồi đã tài liệu hóa và không làm lộ thông tin nhạy cảm.
- OpenAPI mô tả đúng hợp đồng và hành vi bảo mật đã triển khai.
- Test tập trung bao phủ các trường hợp thành công và thất bại đã được duyệt.
- Không commit secrets hoặc thông tin riêng theo môi trường.

## Xác minh

- Chạy các project test backend liên quan đến xác thực.
- Build project API.
- Kiểm tra tài liệu OpenAPI được tạo và xác nhận nội dung khớp với hợp đồng đã triển khai.
- Ghi lại lệnh và kết quả thực tế trong task này.

## Ghi chú triển khai

Đã triển khai bốn endpoint auth theo hợp đồng trong `spec.md`: đăng nhập, refresh, đăng xuất và lấy người dùng hiện tại. Backend dùng ASP.NET Core Identity, JWT access token, refresh session lưu hash, refresh cookie HttpOnly, kiểm tra quyền theo app/role, khóa tài khoản khi đăng nhập sai nhiều lần, CORS allow-list và seed tài khoản local theo cấu hình.

Thông tin JWT signing key và mật khẩu seed không còn nằm trong tệp cấu hình được commit. Local Development dùng .NET user secrets; integration tests tạo signing key và seed password ngẫu nhiên trong cấu hình runtime.

OpenAPI trước đây áp dụng Bearer ở cấp toàn tài liệu, khiến cả endpoint cho phép anonymous bị mô tả như cần đăng nhập. Đã chuyển sang gắn Bearer theo metadata authorization của từng endpoint và thêm integration test xác nhận login/refresh/logout là anonymous còn `/me` yêu cầu Bearer.

Integration test refresh trước đây yêu cầu access token phải khác ngay sau khi refresh; hai JWT có thể giống nhau nếu được tạo trong cùng một giây. Test đã sửa để xác minh refresh cookie thực sự được xoay vòng theo hợp đồng.

Đã nâng `Microsoft.EntityFrameworkCore.Sqlite` lên `10.0.14`; phiên bản SQLitePCLRaw đi kèm không còn nằm trong dải bị cảnh báo bởi advisory GHSA-2m69-gcr7-jv3q.

## Bằng chứng xác minh

- `dotnet restore .\CafeCR.slnx` — thành công.
- `dotnet test .\CafeCR.slnx --no-restore` — thành công; 16 unit tests và 15 integration tests đạt, không có test bị bỏ qua.
- `dotnet build .\backend\src\ChonRieng.Api\ChonRieng.Api.csproj --no-restore` — thành công, 0 cảnh báo, 0 lỗi.
- `dotnet list .\backend\src\ChonRieng.Infrastructure\ChonRieng.Infrastructure.csproj package --vulnerable --include-transitive` — không tìm thấy package có lỗ hổng.
- Integration tests xác minh luồng login thành công/thất bại, role-app access, phone normalization, account lockout, refresh rotation, logout idempotent, `/me`, CORS credentials, cookie flags và yêu cầu Bearer theo từng endpoint OpenAPI.
