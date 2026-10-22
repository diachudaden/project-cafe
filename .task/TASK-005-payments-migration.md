# TASK-005: Tích hợp EF Core và tạo, kiểm tra, áp dụng migration Payments

- Trạng thái: Bị chặn ở bước áp dụng
- Mục tiêu: Tạo và rà soát migration Payments, rồi chỉ áp dụng lên đúng database đã được xác nhận và có schema phù hợp.
- Trở ngại: Do repo không có ứng dụng/database model thật, project và migration đã được scaffold dùng các entity mẫu. Migration tạo cả bảng `Bills`, `Seats`, `Sessions`, `Shifts` cùng các bảng payment; do đó chỉ phù hợp database trống theo schema bootstrap này. Không áp dụng lên database hiện hữu khi chưa đối chiếu schema và môi trường.

## Tài liệu tham chiếu

- [../spec.md](../spec.md)
- [../docs/payments/SPEC.md](../docs/payments/SPEC.md), đặc biệt mục 9 về Ledger và mục 15 về Definition of Done
- [../README.md](../README.md), mục “Nối vào dự án” và “Migration”
- [../.rule/architecture.md](../.rule/architecture.md)
- [../.rule/security.md](../.rule/security.md)
- [../.github/instructions/backend.instructions.md](../.github/instructions/backend.instructions.md)

## Phụ thuộc

- TASK-002: tích hợp module thanh toán với domain, persistence và ứng dụng thực tế.
- TASK-004: cấu hình SQL local an toàn và bảo đảm runtime EF Core nhận được connection string mà không đưa secret vào Git.

## Các bước thực hiện

1. **Xác nhận ứng dụng đích**
   - Xác định đúng project Infrastructure, API startup project, framework, EF Core version, SQL provider, DbContext và cấu hình design-time.
   - Xác nhận `.env` được nạp qua runtime/loader thực tế; .NET không tự nạp `.env`.
   - Xác nhận rõ database đích là local/dev hoặc môi trường không production trước khi áp dụng thay đổi schema.

2. **Nối cấu hình entity vào model**
   - Đăng ký `PaymentTransactionConfiguration`, `LedgerEntryConfiguration` và `ReconciliationIssueConfiguration` trong `AppDbContext.OnModelCreating` hoặc cơ chế cấu hình entity hiện có.
   - Hoàn tất các điểm `INTEGRATE` cần thiết trong `EfPaymentStore` và mapping `Seat` theo entity thật của ứng dụng; không tự đoán entity/namespace.
   - Xác minh khóa, kiểu dữ liệu, index, check constraint, quan hệ và hành vi delete khớp đặc tả; giữ `LedgerEntries` append-only.

3. **Tạo migration**
   - Chạy lệnh scaffold từ repo root sau khi xác nhận đúng tên project:
     ```powershell
     dotnet ef migrations add AddPayments `
       --project <duong-dan-project-Infrastructure.csproj> `
       --startup-project <duong-dan-project-Api.csproj>
     ```
   - Nếu repository dùng migration project hoặc `DbContext` cụ thể khác, bổ sung `--context`/`--project` theo cấu hình thực tế, không sao chép placeholder.

4. **Rà soát migration được tạo**
   - Kiểm tra migration và model snapshot; xác nhận tạo các bảng `PaymentTransactions`, `LedgerEntries`, `ReconciliationIssues`, các index/constraint theo cấu hình và các cột giữ chỗ ghế cần thiết.
   - Đảm bảo migration không xóa/đổi dữ liệu hoặc schema hiện có ngoài phạm vi task.
   - Build project và chạy các test backend liên quan.

5. **Áp dụng có kiểm soát**
   - Trước khi cập nhật, xác minh database đích bằng metadata kết nối an toàn; không in connection string hoặc credential ra log/terminal.
   - Chỉ chạy cập nhật trên database local/dev trống, đã xác nhận phù hợp schema bootstrap:
     ```powershell
     dotnet ef database update `
       --project <duong-dan-project-Infrastructure.csproj> `
       --startup-project <duong-dan-project-Api.csproj>
     ```
   - Không chạy lên production nếu chưa có xác nhận rõ ràng và quy trình backup/triển khai phù hợp.
   - Xác nhận các bảng/index cần thiết tồn tại và ghi lại lệnh cùng kết quả, không ghi secret.

## Tiêu chí hoàn thành

- EF Core nhận đúng DbContext và SQL provider từ ứng dụng thật.
- Migration `AddPayments` được tạo và rà soát; hiện là migration khởi tạo schema bootstrap theo entity tham khảo, không phải migration gia tăng cho database đã tồn tại.
- Backend build và các kiểm tra liên quan chạy thành công.
- Migration được áp dụng thành công lên đúng database không production đã xác nhận, hoặc task vẫn ghi rõ bị chặn trước bước này.
- Không có credential trong source, migration, task, log hoặc Git.

## Bằng chứng và trạng thái thực hiện

- Đã tạo solution `CafeCR.slnx` và project Domain/Application/Infrastructure .NET 10 với SQL Server provider, `AppDbContext` và design-time factory.
- Entity Bill/Seat/Session/Shift là bootstrap theo mẫu trong tài liệu; cần thay/đối chiếu với domain thật trước production.
- `.env` có khóa `ConnectionStrings__DefaultConnection`; định dạng provider được nhận diện là SQL Server mà không in giá trị. Design-time factory đọc biến môi trường, còn script EF nạp biến từ `.env` cho tiến trình con.
- Đã cài `dotnet-ef` local 10.0.5 vào manifest `dotnet-tools.json`.
- Đã chạy `dotnet ef migrations add AddPayments`; migration và snapshot được tạo tại `backend/src/ChonRieng.Infrastructure/Migrations`.
- Đã thêm `tools/payments/ef-migrations.ps1` để nạp `ConnectionStrings__DefaultConnection` từ `.env` mà không in giá trị, sau đó scaffold migration, sinh script idempotent hoặc yêu cầu xác nhận tường minh trước khi update.
- Đã chạy script tạo SQL idempotent tại `backend/src/ChonRieng.Infrastructure/Migrations/AddPayments.sql`; rà soát cho thấy script chỉ tạo `__EFMigrationsHistory`, `PaymentTransactions`, `ReconciliationIssues`, `Seats`, `Sessions`, `Shifts`, `LedgerEntries`, `Bills`; không có `DROP TABLE`/`DROP COLUMN`, không chứa connection string.
- Đã kiểm tra chốt an toàn: gọi `-Action Update` khi thiếu xác nhận bị chặn trước khi chạy EF.
- Đã thêm project test xUnit và kiểm thử mapping EF Core cho bảng payment và row-version của Seat; `dotnet test backend/tests/ChonRieng.UnitTests/ChonRieng.UnitTests.csproj --filter "FullyQualifiedName~Payments"`: 61/61 test thành công.
- Đã build `CafeCR.slnx` thành công. Restore ban đầu báo advisory cho `System.Security.Cryptography.Pkcs`; đã nâng lên bản vá 9.0.21. Kiểm tra `dotnet list ... package --vulnerable --include-transitive` không còn package có advisory; build sau cập nhật thành công, 0 warning và 0 error.
- Chưa áp dụng migration và chưa kết nối tới database: migration hiện tạo cả các bảng domain mẫu, nên cần xác nhận database đích trống/dev và phù hợp trước khi ghi schema.
- Phát hiện `.env` đang được Git theo dõi. Đã thêm quy tắc ignore cho `.env` và `.env.*` (ngoại lệ `.env.example`), đồng thời gỡ `.env` khỏi Git index bằng `git rm --cached`; file local được giữ nguyên. Việc bỏ theo dõi này đang ở trạng thái staged, chưa commit.
- Không ghi credential vào log/file; chỉ đọc biến cần thiết vào process environment khi scaffold, không kết nối database.
