# TASK-002: Tích hợp module thanh toán với domain và persistence thực tế

- Trạng thái: Bị chặn
- Mục tiêu: Nối module Payments vào ứng dụng .NET thật, dùng đúng entity/domain, DbContext và SQL provider của dự án.
- Trở ngại: Đã dựng project Domain/Application/Infrastructure và `AppDbContext` SQL Server dùng entity tham khảo để có thể scaffold database khởi tạo. Tuy nhiên workspace vẫn chưa có API `Program.cs`, authentication, domain thật hoặc schema database hiện hữu. Không thể xem bootstrap entity là mô hình production; cần ghép với ứng dụng thật trước khi deploy hoặc dùng database đã có dữ liệu.

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

4. **Nối API và đăng ký dịch vụ**
   - Đăng ký `AddPayments`, map endpoint và cấu hình SignalR/job theo startup hiện tại.
   - Thay role, claim, hub và kiểm tra quyền sở hữu bill bằng giá trị đúng của ứng dụng.
   - Bảo đảm webhook vẫn đọc raw body trước khi xác thực HMAC và tuân thủ quy tắc phản hồi trong đặc tả.

5. **Kiểm tra**
   - Build các project backend liên quan và chạy unit test Payments.
   - Ghi lại các lệnh và kết quả thực tế; xử lý lỗi phát sinh trong phạm vi tích hợp.
   - Chỉ chuyển TASK-004 sang thực hiện khi đã xác định provider và cấu hình database thật.

## Tiêu chí hoàn thành

- Module được đăng ký và chạy trong API thật mà không còn điểm `INTEGRATE` cần thiết bị bỏ ngỏ.
- Entity, DbContext, giao dịch và API phù hợp với domain hiện có và `docs/payments/SPEC.md`.
- Build và test liên quan thành công; có bằng chứng lệnh chạy.
- Không đưa credential hoặc giá trị môi trường riêng vào source, task hay log.

## Bằng chứng và trạng thái thực hiện

- Đã tạo `CafeCR.slnx`, các project Domain/Application/Infrastructure và DbContext để chạy migration cho schema khởi tạo.
- Các entity Bill/Seat/Session/Shift được dựng theo mẫu tài liệu `ReferenceEntities.cs`, không phải xác nhận về schema production.
- Chưa tạo API host/authentication vì quy tắc đăng nhập, roles và cấu hình ứng dụng thực tế không được cung cấp.
- Cần tích hợp/đối chiếu domain và schema thật trước khi dùng trên database hiện hữu.
