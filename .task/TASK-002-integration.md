# TASK-002: Tích hợp module thanh toán với domain và persistence thực tế

- Trạng thái: Đang làm
- Mục tiêu: Nối module Payments vào ứng dụng .NET thật, dùng đúng entity/domain, DbContext và SQL provider của dự án.
- Tiến độ/trở ngại: Đã dựng project Domain/Application/Infrastructure và `AppDbContext` SQL Server dùng entity tham khảo. Bổ sung API host/Swagger theo cấu hình local fail-closed; domain và schema vẫn là mẫu, auth cần cấu hình issuer/audience/key thật trước khi sử dụng token và triển khai.

## Tài liệu tham chiếu

- [../spec.md](../spec.md)
- [../docs/payments/SPEC.md](../docs/payments/SPEC.md)
- [../docs/payments/PROMPT.md](../docs/payments/PROMPT.md)
- [../README.md](../README.md)
- [../.rule/architecture.md](../.rule/architecture.md)
- [../.rule/business-rules.md](../.rule/business-rules.md)
- [../.github/instructions/backend.instructions.md](../.github/instructions/backend.instructions.md)

## Phụ thuộc

- Cần đưa đúng source backend của ứng dụng vào workspace hoặc cung cấp đường dẫn project có thể truy cập.
- Cần xác nhận SQL provider và cách chạy local; không cần chia sẻ credential trong chat.

## Các bước thực hiện

1. **Xác định cấu trúc backend thật**
   - Tìm project API/Infrastructure, `Program.cs`, `DbContext`, entity Bill/Seat/Session/Shift, cấu hình role/claim và SignalR hub.
   - Ghi nhận target framework, phiên bản EF Core/provider và cách chạy/test hiện tại.

2. **Nối domain**
   - Cho Bill implement `IPayableBill` và Seat implement `IHoldableSeat` theo entity thật, giữ nguyên quy tắc nghiệp vụ hiện tại.
   - Thêm các trường giữ chỗ/row-version cần thiết vào Seat và mapping tương ứng.
   - Không đổi luồng tạo bill hoặc phiên ngồi ngoài yêu cầu thanh toán trước rồi mới tạo phiên theo đặc tả.

3. **Nối Infrastructure và DbContext**
   - Chỉnh các điểm `INTEGRATE` trong store theo namespace, entity và quy tắc giao dịch của ứng dụng.
   - Đăng ký entity configurations trong DbContext theo cơ chế hiện có.
   - Kiểm tra toàn bộ thao tác bill/ghế/ledger của webhook nằm trong cùng một `SaveChangesAsync`; SignalR chỉ phát sau commit; `LedgerEntries` giữ append-only.

4. **Tạo API host và Swagger**
   - Tạo ASP.NET Core API project, đăng ký `AppDbContext`, authentication fail-closed, Swagger/OpenAPI, SignalR và `AddPayments`.
   - Map payment endpoints và POS hub; không bật recurring jobs cho tới khi Hangfire storage được cấu hình.
   - Thêm cấu hình local không chứa secret và script nạp `.env` mà không in credential.
   - Swagger chỉ bật trong Development; không tạo đăng nhập giả hoặc endpoint bỏ qua authorization.
   - Đối chiếu roles/claims với identity provider thật trước khi triển khai.
   - Bảo đảm webhook vẫn đọc raw body trước khi xác thực HMAC và tuân thủ quy tắc phản hồi trong đặc tả.

5. **Kiểm tra**
   - Build các project backend liên quan và chạy unit test Payments.
   - Ghi lại các lệnh và kết quả thực tế; xử lý lỗi phát sinh trong phạm vi tích hợp.
   - Chỉ chuyển TASK-004 sang thực hiện khi đã xác định provider và cấu hình database thật.

## Tiêu chí hoàn thành

- Module được đăng ký và chạy trong API thật mà không còn điểm `INTEGRATE` cần thiết bị bỏ ngỏ.
- Entity, DbContext, giao dịch và API phù hợp với domain hiện có và `docs/payments/SPEC.md`.
- API khởi động bằng Development config; Swagger trả HTTP 200 và OpenAPI liệt kê webhook/payment routes.
- Endpoint được bảo vệ không chấp nhận request không có JWT hợp lệ; production từ chối khởi động nếu thiếu issuer/audience/signing key an toàn.
- Build và test liên quan thành công; có bằng chứng lệnh chạy.
- Không đưa credential hoặc giá trị môi trường riêng vào source, task hay log.

## Bằng chứng và trạng thái thực hiện

- Đã tạo `CafeCR.slnx`, các project Domain/Application/Infrastructure và DbContext để chạy migration cho schema khởi tạo.
- Các entity Bill/Seat/Session/Shift được dựng theo mẫu tài liệu `ReferenceEntities.cs`, không phải xác nhận về schema production.
- Đã tạo API host `ChonRieng.Api`, Swagger, JWT Bearer và SignalR hub. Trong Development, signing key chưa cấu hình được sinh ngẫu nhiên theo tiến trình; endpoint vẫn fail-closed. Ngoài Development yêu cầu issuer/audience/signing key >= 32 byte.
- API host dùng `.env` qua script chạy local; không tự chạy migration hoặc mở kết nối database lúc khởi động.
- Job Hangfire chưa bật vì chưa có storage Hangfire.
- Đã build API thành công; smoke test bằng connection string giả localhost: Swagger UI HTTP 200, OpenAPI 3.0.1, 11 routes gồm SePay webhook và tạo QR.
- Smoke test endpoint tạo QR không có JWT trả HTTP 401; OpenAPI công bố Bearer security scheme.
- Xác minh lại: `dotnet test ... --filter "FullyQualifiedName~Payments"` pass 61/61; `dotnet build CafeCR.slnx --no-restore` thành công 0 warning/0 error; `git diff --check` pass.
- Package scan API không phát hiện vulnerable packages sau khi chọn phiên bản Swagger/JSON đã vá.
- Cần tích hợp/đối chiếu domain và schema thật trước khi dùng trên database hiện hữu.
