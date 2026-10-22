# Module Thanh toán Chốn Riêng — VietQR + SePay + Sổ thu chi

Gói này chứa toàn bộ tính năng đã chốt:

1. Tạo mã VietQR cho bill, **giữ chỗ** 10 phút trong lúc chờ.
2. Khách quét bằng app ngân hàng bất kỳ (Techcombank…), chuyển tiền thật.
3. SePay báo về qua webhook (xác thực HMAC-SHA256) → bill **Paid** → POS kêu **"tink tink"** + đọc "Đã nhận X đồng".
4. **Tự xếp chỗ** như trên đơn, không còn thì chọn chỗ mặc định cùng loại.
5. Ghi **sổ thu chi** append-only, báo cáo ngày/tuần/tháng/quý/năm, đảo bút toán, đối soát cuối ngày.
6. Xử lý thủ công: khách xoá mã, chuyển thiếu, chuyển 2 lần, thu tiền mặt.

Cấu trúc thư mục **giống hệt repo** — chép đè cả thư mục vào gốc repo là các file nằm đúng chỗ.

```
.claude/skills/chonrieng-payments/SKILL.md   ← skill cho Claude Code (tự nạp khi làm việc về thanh toán)
.cursor/rules/payments.mdc                    ← rule cho Cursor
docs/payments/SPEC.md                         ← nguồn sự thật: luồng, quy tắc, API, DoD
docs/payments/PROMPT.md                       ← prompt dán vào AI tool để nó nối module vào code hiện có
docs/payments/reference/ReferenceEntities.cs  ← mẫu Bill/Seat/Session/Shift implement interface (chỉ tham khảo)
backend/src/ChonRieng.Domain/Payments/        ← entity, enum, mã thanh toán, interface IPayableBill/IHoldableSeat
backend/src/ChonRieng.Application/Payments/   ← PaymentService, LedgerService, SeatAllocator, verifier HMAC, jobs
backend/src/ChonRieng.Infrastructure/Payments/← EF config + store
backend/src/ChonRieng.Api/Payments/           ← endpoints, SignalR notifier, AddPayments/MapPaymentEndpoints/UsePaymentJobs
backend/tests/ChonRieng.UnitTests/Payments/   ← 59 test case
frontend/packages/api-client/src/payments.ts  ← kiểu dữ liệu + client
frontend/apps/pos/src/features/payments/      ← hộp thoại QR, âm báo, xử lý thủ công, trang thu chi
tools/payments/send-fake-webhook.mjs          ← gửi webhook giả có chữ ký đúng để test local
```

## Trạng thái hiện tại

| Phần | Trạng thái |
| --- | --- |
| Backend/API | `dotnet build CafeCR.slnx --no-restore` thành công trên .NET 10, 0 warning và 0 error |
| Unit test Payments | 61/61 pass |
| API/Swagger | Smoke test local: Swagger UI HTTP 200, OpenAPI 3.0.1, 11 routes; endpoint payment không có JWT trả 401 |
| EF Core/migration | Infrastructure build được; migration bootstrap đã scaffold nhưng **chưa áp dụng** lên database |
| Domain/schema | Entity hiện tại là mẫu bootstrap; chưa xác nhận khớp domain hoặc schema production |
| Jobs | Chưa bật recurring jobs vì chưa cấu hình Hangfire storage |

## Tích hợp vào domain/backend thực tế — 7 bước

Repo hiện có backend bootstrap và API host để chạy thử; các bước dưới đây vẫn cần hoàn tất trước khi xem đây là tích hợp production. Không áp dụng migration hiện tại lên database đang có dữ liệu.

### 1. Cho entity hiện có implement interface

Tìm các comment `// INTEGRATE:` trong code — đó là toàn bộ chỗ cần nối. Chính:

- `Bill : IPayableBill` và `Seat : IHoldableSeat` — xem mẫu trong `docs/payments/reference/ReferenceEntities.cs`.
- `Seat` thêm 3 cột: `HeldForBillId`, `HeldUntil`, `RowVersion` (`IsRowVersion()`), và trạng thái `Held`.
- `EfPaymentStore.StartSessionAsync`: tạo `Session` theo cách của dự án. Luồng mới là **trả tiền trước rồi mới tạo phiên ngồi**.
- Sửa `using` namespace `AppDbContext`, `PosHub`, tên role, claim user id.

### 2. Gói NuGet (nếu project chưa có)

```bash
dotnet add backend/src/ChonRieng.Application package Microsoft.Extensions.Options
dotnet add backend/src/ChonRieng.Application package Microsoft.Extensions.Logging.Abstractions
dotnet add backend/src/ChonRieng.Infrastructure package Microsoft.Extensions.Options.DataAnnotations
dotnet add backend/src/ChonRieng.Infrastructure package Microsoft.Extensions.Options.ConfigurationExtensions
# Hangfire.Core đã có trong stack
```

Code dùng `ImplicitUsings` (mặc định của template .NET) và `Nullable` bật.

### 3. API host và `Program.cs`

API host hiện đã đăng ký `AppDbContext`, authentication JWT, Swagger trong Development, SignalR và module Payments. Khi ghép vào API/domain thực tế, hợp nhất đăng ký và route với cấu hình hiện có thay vì tạo host trùng lặp:

```csharp
using ChonRieng.Api.Payments;

builder.Services.AddPayments(builder.Configuration);
// ...
app.MapPaymentEndpoints();
app.UsePaymentJobs();   // đặt sau khi Hangfire đã cấu hình
```

Recurring jobs hiện chưa được bật vì chưa có Hangfire storage. `PosHub.OnConnectedAsync` phải cho connection vào group `branch-{id}` và `managers` — mẫu trong `SignalRPaymentNotifier.cs`. Đối chiếu role, claims và identity provider trước khi triển khai.

### 4. Cấu hình

`appsettings.json`:

```json
"Payment": {
  "Bank": "MBBank",
  "AccountNumber": "0123456789",
  "AccountHolder": "NGUYEN VAN A",
  "CodePrefix": "CR",
  "DescriptionPrefix": "",
  "SeatHoldMinutes": 10,
  "QrTemplate": "compact"
}
```

Secret **không** để trong file:

```bash
# local
dotnet user-secrets set "Payment:WebhookSecret" "<secret từ SePay>" --project backend/src/ChonRieng.Api
# docker-compose: thêm vào .env (đã gitignore) rồi map vào service api
Payment__WebhookSecret=<secret từ SePay>
```

`DescriptionPrefix = "SEVQR"` nếu tài khoản nhận là VietinBank cá nhân.

### 5. Migration

Backend hiện có project EF Core SQL Server tối thiểu để scaffold schema. Tạo migration và script SQL idempotent bằng PowerShell:

```powershell
.\tools\payments\ef-migrations.ps1 -Action Add
.\tools\payments\ef-migrations.ps1 -Action Script
```

Script nạp `ConnectionStrings__DefaultConnection` từ `.env` vào tiến trình con, không in giá trị ra terminal. .NET không tự đọc `.env`.

**Lưu ý:** do repo chưa có domain/database model thật của ứng dụng, migration hiện tại dùng các entity mẫu trong `backend/src/ChonRieng.Domain/Entities/ReferenceEntities.cs`. Đây là schema khởi tạo cho database trống, gồm cả `Bills`, `Seats`, `Sessions` và `Shifts` cùng các bảng Payments. Không áp dụng migration này vào database đã có các bảng đó; trước tiên phải ghép model/migration với schema thực tế và rà soát file SQL.

Chưa chạy `database update` tự động: migration bootstrap tạo toàn bộ bảng trong mô hình mẫu, nên cần xác nhận database đích là database local/dev trống trước khi áp dụng. Không chạy migration này lên database đang có dữ liệu hoặc bảng ứng dụng. Sau khi tự rà soát SQL và xác minh database đích là database trống/dev, lệnh update có các chốt xác nhận:

```powershell
.\tools\payments\ef-migrations.ps1 -Action Update `
  -ConfirmTargetDatabase "<tên database trong .env>" `
  -ConfirmEmptyDevelopmentDatabase
```

Script từ chối nếu thiếu xác nhận tên database, tên trong `.env` không khớp hoặc tên có dấu hiệu production/live. Người chạy vẫn phải tự xác minh database đúng là môi trường development và đang trống.

### 6. Frontend

```tsx
// layout POS
import { PaymentsProvider, usePaymentEvents } from './features/payments';

<PaymentsProvider fetcher={apiFetch} connection={posHub}>
  <PosShell />
</PaymentsProvider>

function PosShell() {
  usePaymentEvents((t) => toast[t.kind](t.title, { description: t.detail })); // nối vào toast của packages/ui
  // ...
}

// khi check-in xong một bill
<PaymentQrDialog billId={bill.id} onPaid={() => refetchSeats()} onClose={close} />

// trang quản lý
<AttentionPanel />
<LedgerPage branchId={branchId} />
```

Âm báo tự tổng hợp bằng Web Audio, không cần file mp3. Trình duyệt chỉ cho phát sau lần chạm/click đầu tiên trên trang — `usePaymentEvents` đã tự xử lý. Đọc số tiền cần máy có giọng tiếng Việt (Chrome trên Windows/Android thường có), không có thì vẫn kêu tink tink.

### 7. Cấu hình SePay và test

1. my.sepay.vn → liên kết tài khoản nhận (nên là tài khoản **riêng cho demo**, ngân hàng không bắt buộc VA: MB, VCB, ACB, TPBank…).
2. **Cấu hình chung → Cấu trúc mã thanh toán**: tiền tố `CR`.
3. Làm trong **Test mode** trước: tạo webhook URL `https://<ngrok>/api/webhooks/sepay`, loại *Tiền vào*, JSON, bật *Chỉ gửi khi có mã thanh toán*, bảo mật **HMAC-SHA256**, copy Secret vào user-secrets.
4. Chạy `ngrok http 8080` (hoặc `cloudflared tunnel`) để SePay gọi được máy bạn.
5. Test không tốn tiền:
   ```bash
   SEPAY_WEBHOOK_SECRET=<secret> node tools/payments/send-fake-webhook.mjs --code CR<billId> --amount 25000
   SEPAY_WEBHOOK_SECRET=<secret> node tools/payments/send-fake-webhook.mjs --code CR<billId> --amount 25000 --id 99 --repeat 3
   SEPAY_WEBHOOK_SECRET=<secret> node tools/payments/send-fake-webhook.mjs --content "ck tien cafe" --amount 25000
   ```
6. Demo thật: tạo webhook Live (URL HTTPS hợp lệ), tạo bill 2.000đ, quét bằng Techcombank.

## Chạy test

```bash
dotnet test backend/tests/ChonRieng.UnitTests --filter "FullyQualifiedName~Payments"
```

## Chạy API và Swagger local

1. Đổi SQL password nếu credential từng được chia sẻ, rồi cập nhật `.env` local. Không commit file này.
2. Chạy từ PowerShell tại repo root:

   ```powershell
   .\tools\payments\run-api.ps1
   ```

3. Mở `http://localhost:8080/swagger`.

Swagger chỉ bật trong môi trường `Development`. API yêu cầu `ConnectionStrings__DefaultConnection`; script nạp biến đó từ `.env` vào tiến trình API mà không in giá trị. .NET không tự nạp `.env`.

Các giá trị Payment trong `appsettings.Development.json` chỉ là placeholder để khởi động Swagger; cần cấu hình thông tin ngân hàng và `Payment__WebhookSecret` trước khi thử thanh toán. JWT auth dùng signing key ngẫu nhiên theo tiến trình trong Development nếu chưa cấu hình, nên không có token đăng nhập hợp lệ giữa các lần chạy. Các endpoint yêu cầu authorization vẫn trả 401 cho request không có token hợp lệ.

Ngoài Development, phải cấu hình `Authentication__Issuer`, `Authentication__Audience` và `Authentication__SigningKey` dài ít nhất 32 byte bằng secret manager/environment của môi trường. Chưa cấu hình Hangfire storage nên recurring payment jobs chưa được bật. API có thể khởi động và phục vụ Swagger mà không kết nối database ngay lúc startup; chỉ thao tác persistence mới mở kết nối.

Project API và entity hiện là bootstrap theo module thanh toán, chưa đại diện cho domain production. Đối chiếu schema và identity provider thật trước khi triển khai hoặc áp dụng migration lên database hiện hữu.

Nếu chép module sang repo khác, test project cần `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk` và tham chiếu tới các project tương ứng.

## Những điểm dễ sai

- Webhook phải đọc **raw body** rồi mới verify. Đừng thêm `[FromBody]`.
- Webhook đã xác thực thì luôn trả `200 {"success": true}` — trả lỗi chỉ làm SePay gửi lại mãi.
- Không bao giờ sửa/xoá `LedgerEntries`. Sai thì bấm **Đảo**.
- Báo cáo đọc từ `LedgerEntries`, không cộng từ `Bills`.
- Không commit `WebhookSecret`.
