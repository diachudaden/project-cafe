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

## Đã kiểm tra tới đâu

| Phần | Trạng thái |
| --- | --- |
| Domain, Application, Api | Build sạch trên **.NET 10**, bật warnings-as-errors, chạy với entity mẫu trong `docs/payments/reference` |
| Unit test | **59/59 pass** (mã thanh toán, HMAC, khớp bill, chống trùng, thiếu/dư tiền, xếp chỗ, nhả ghế, sổ thu chi, báo cáo theo kỳ, đối soát) |
| Webhook end-to-end | Chạy API thật + `send-fake-webhook.mjs`: chữ ký sai → 401; gửi 3 lần cùng id → 1 dòng sổ; khách xoá mã → cần xử lý |
| Frontend | `tsc --strict` sạch với React 19, TanStack Query 5, @microsoft/signalr 8 |
| **Infrastructure (EF Core)** | **Chưa build được ở đây** (môi trường không tải được gói EF). Đây là tầng duy nhất cần bạn/AI tool build và sửa khi nối vào `AppDbContext` thật |

## Nối vào dự án — 7 bước

> Cách nhanh: chép thư mục vào repo rồi dán nội dung `docs/payments/PROMPT.md` cho Claude Code hoặc Cursor. Nó sẽ làm các bước dưới.

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

### 3. Program.cs

```csharp
using ChonRieng.Api.Payments;

builder.Services.AddPayments(builder.Configuration);
// ...
app.MapPaymentEndpoints();
app.UsePaymentJobs();   // đặt sau khi Hangfire đã cấu hình
```

`PosHub.OnConnectedAsync` phải cho connection vào group `branch-{id}` và `managers` — mẫu trong `SignalRPaymentNotifier.cs`.

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

```bash
dotnet ef migrations add AddPayments -p backend/src/ChonRieng.Infrastructure -s backend/src/ChonRieng.Api
dotnet ef database update -p backend/src/ChonRieng.Infrastructure -s backend/src/ChonRieng.Api
```

Tạo bảng `PaymentTransactions`, `LedgerEntries`, `ReconciliationIssues` và thêm cột giữ chỗ cho `Seats`.

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

Test project cần `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk` (thường đã có) và tham chiếu tới `ChonRieng.Application`.

## Những điểm dễ sai

- Webhook phải đọc **raw body** rồi mới verify. Đừng thêm `[FromBody]`.
- Webhook đã xác thực thì luôn trả `200 {"success": true}` — trả lỗi chỉ làm SePay gửi lại mãi.
- Không bao giờ sửa/xoá `LedgerEntries`. Sai thì bấm **Đảo**.
- Báo cáo đọc từ `LedgerEntries`, không cộng từ `Bills`.
- Không commit `WebhookSecret`.
