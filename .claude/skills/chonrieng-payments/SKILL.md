---
name: chonrieng-payments
description: Dùng khi làm bất cứ thứ gì liên quan tới thanh toán, VietQR, SePay webhook, giữ chỗ/xếp chỗ sau thanh toán, hoặc sổ thu chi (LedgerEntries) trong dự án Chốn Riêng.
---

# Module thanh toán Chốn Riêng

Nguồn sự thật là `docs/payments/SPEC.md`. Đọc nó trước khi sửa bất kỳ file nào dưới đây.

## Code nằm ở đâu

| Tầng | Thư mục |
| --- | --- |
| Domain | `backend/src/ChonRieng.Domain/Payments/` |
| Application | `backend/src/ChonRieng.Application/Payments/` |
| Infrastructure | `backend/src/ChonRieng.Infrastructure/Payments/` |
| Api | `backend/src/ChonRieng.Api/Payments/` |
| Tests | `backend/tests/ChonRieng.UnitTests/Payments/` |
| Frontend | `frontend/apps/pos/src/features/payments/`, `frontend/packages/api-client/src/payments.ts` |

Đăng ký DI: `services.AddPayments(configuration)` trong `Program.cs`,
map endpoint: `app.MapPaymentEndpoints()`.

## Luật không được phá

1. **Webhook đọc raw body** rồi mới verify HMAC. Không thêm `[FromBody]`, không parse trước.
2. Chuỗi ký là `"{timestamp}.{raw_body}"`, header `X-SePay-Signature: sha256={hex}`. So sánh constant-time.
3. Webhook đã xác thực thì **luôn** trả `200 {"success": true}`, kể cả khi bỏ qua giao dịch. Chỉ 401 khi sai chữ ký.
4. **Chống trùng** bằng unique index `PaymentTransactions.SepayId`. Bắt lỗi unique violation → coi như đã xử lý.
5. `LedgerEntries` là **append-only**. Không viết `Update`/`Remove` cho entity này. Sửa sai = `LedgerEntry.Reverse(...)`.
6. Mọi thay đổi bill + ghế + ledger của một webhook nằm trong **một** `SaveChangesAsync`. SignalR đẩy **sau** commit.
7. Tiền là `long` VND. Thời gian lưu UTC.
8. Không commit `WebhookSecret`. Đọc từ user-secrets / env `Payment__WebhookSecret`.
9. Báo cáo thu chi chỉ đọc từ `LedgerEntries`, không cộng từ `Bills`.

## Khi thêm tính năng mới

- Thêm cổng thanh toán khác (MoMo...): implement `IPaymentProvider`, đăng ký thêm, không sửa `PaymentService`.
- Thêm cách chọn ghế mặc định: implement `IDefaultSeatStrategy`.
- Thêm loại thu/chi: thêm giá trị vào `LedgerCategory`, cập nhật `LedgerCategoryLabels` ở frontend.

## Cách test nhanh

```bash
dotnet test backend/tests/ChonRieng.UnitTests --filter "FullyQualifiedName~Payments"
# gửi webhook giả có chữ ký đúng tới local:
node tools/payments/send-fake-webhook.mjs --code CR1 --amount 25000
```

Trước khi báo xong: chạy test ở trên, và tự kiểm theo mục "Definition of Done" trong SPEC.
