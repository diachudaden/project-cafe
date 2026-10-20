# SPEC — Module Thanh toán VietQR + SePay (Chốn Riêng)

> Tài liệu nguồn cho cả người và AI coding tool (Claude Code / Cursor).
> Mọi quyết định thiết kế của module thanh toán nằm ở đây. Code phải bám theo spec này.

## 1. Mục tiêu

Khách (hoặc nhân viên POS) tạo bill → hệ thống hiện mã VietQR → khách quét bằng app ngân hàng bất kỳ
(ví dụ Techcombank) và chuyển tiền thật → SePay báo về qua webhook → hệ thống:

1. Đánh dấu bill đã thanh toán.
2. Xếp chỗ ngồi: chỗ khách đã chọn nếu còn giữ, không thì chỗ mặc định cùng loại.
3. Ghi một dòng **Thu** vào sổ cái thu chi (`LedgerEntries`).
4. Đẩy sự kiện realtime về POS: phát "tink tink" + đọc "Đã nhận X đồng" + hiện số bàn.

## 2. Phạm vi

| Có | Không |
| --- | --- |
| Tạo URL ảnh VietQR (không gọi API) | Cổng MoMo/ZaloPay (để dành qua `IPaymentProvider`) |
| Nhận webhook SePay, xác thực HMAC-SHA256 | Hoàn tiền tự động (hoàn tay, ghi bút toán đảo) |
| Chống trùng theo `id` giao dịch SePay | Thanh toán thẻ |
| Giữ chỗ tạm khi tạo QR, nhả khi hết hạn | |
| Sổ cái thu chi append-only + bút toán đảo | |
| Xác nhận thủ công khi khách sửa nội dung CK | |
| Đối soát cuối ngày VietQR ↔ SePay | |

## 3. Luồng chính

```
POS/Customer app         API .NET                    vietqr.app     App ngân hàng     SePay
  │ POST /api/payments/bills/{id}/qr                     │               │              │
  │─────────────────────────>│ Bill Pending?              │               │              │
  │                          │ Giữ chỗ (HeldUntil=+10')   │               │              │
  │<── {qrUrl, code, amount, expiresAt}                   │               │              │
  │ <img src=qrUrl> ───────────────────────────────────>│ ảnh QR        │              │
  │                          │                            │  quét + CK ──>│ NH nhận báo ─>│
  │                          │<──── POST /api/webhooks/sepay (HMAC) ────────────────────│
  │                          │ verify → dedup → khớp mã → Bill.MarkPaid               │
  │                          │ → SeatAllocator → Session.Start → LedgerEntry(Thu)      │
  │                          │──── 200 {"success": true} ──────────────────────────────>│
  │<── SignalR PaymentReceived + SeatAssigned  →  tink tink + "Đã nhận 25.000 đồng"
```

## 4. Quy ước

- **Mã thanh toán**: `CR{BillId}` — chỉ chữ + số, không dấu, không khoảng trắng. Ví dụ `CR1024`.
  Tiền tố `CR` cấu hình trong SePay → Cấu hình chung → Cấu trúc mã thanh toán.
  Đọc mã không phân biệt hoa thường. Nếu SePay không tách được `code` thì tìm trong `content`,
  chỉ nhận khi có **đúng một** mã khớp (tránh gán nhầm bill).
- **Enum** trong JSON là **số** (mặc định System.Text.Json), frontend giữ đúng giá trị như C#.
- **Tiền**: `long` (VND), không dùng `decimal` cho số tiền nhận từ SePay. `Bill.Total` nếu là `decimal` thì ép `(long)`.
- **Thời gian**: lưu UTC trong DB; `transactionDate` của SePay là giờ Việt Nam (UTC+7) → chuyển sang UTC khi lưu.
- **Ngân hàng nhận**: tài khoản cá nhân riêng cho demo, ngân hàng không bắt buộc VA (MB, VCB, ACB, TPBank...).
  Tránh OCB/KienLongBank/MSB/BIDV cá nhân (bắt buộc VA). VietinBank cá nhân phải có `SEVQR` trong nội dung.

## 5. Tạo VietQR

URL ảnh, không cần API key:

```
https://vietqr.app/img?acc={AccountNumber}&bank={Bank}&amount={Amount}&des={Code}&template=compact
```

Nếu `Payment:DescriptionPrefix` có giá trị (ví dụ `SEVQR` cho VietinBank) thì `des = "{prefix} {code}"`.

Endpoint: `POST /api/payments/bills/{billId}/qr` — role `Staff` hoặc chủ bill (khách).

- Bill phải `Pending`, nếu không → 409.
- Nếu bill có `SeatId` → giữ chỗ: `Seat.Hold(billId, now + HoldMinutes)`. Chỗ không trống → 409 `SEAT_TAKEN`.
- Trả `{ billId, code, amount, qrUrl, expiresAt }`.
- Gọi lại khi QR còn hạn → trả lại QR cũ (idempotent), gia hạn giữ chỗ.

## 6. Webhook SePay

Endpoint: `POST /api/webhooks/sepay` — `AllowAnonymous`, xác thực bằng HMAC.

### 6.1 Xác thực

Header SePay gửi:
- `X-SePay-Signature: sha256={hex}`
- `X-SePay-Timestamp: {unix seconds}`

Chuỗi ký: `"{timestamp}.{raw_body}"`, HMAC-SHA256 với `Payment:WebhookSecret`.

Quy tắc:
1. Đọc **raw body bytes**, không model binding trước khi verify.
2. `|now - timestamp| > 300s` → 401.
3. So sánh constant-time (`CryptographicOperations.FixedTimeEquals`).
4. Sai → 401 `{ "success": false }`.

### 6.2 Payload

```json
{
  "id": 92704, "gateway": "MBBank", "transactionDate": "2026-10-18 11:08:33",
  "accountNumber": "0123456789", "subAccount": "", "code": "CR1024",
  "content": "CR1024 chuyen tien", "transferType": "in", "description": "...",
  "transferAmount": 25000, "accumulated": 0, "referenceCode": "FT26..."
}
```

`code` có thể `null` khi nội dung không chứa mã hợp lệ.

### 6.3 Xử lý (trong 1 DB transaction)

1. `transferType != "in"` → bỏ qua, trả success.
2. `accountNumber` khác tài khoản cấu hình → log cảnh báo, bỏ qua, trả success.
3. **Chống trùng**: insert `PaymentTransactions(SepayId UNIQUE)`. Trùng → trả success, không làm gì thêm.
4. Khớp bill:
   - `code` null hoặc không parse được → `MatchStatus = Unmatched`.
   - Bill không tồn tại → `Unmatched`.
   - Bill đã `Paid` → `Duplicate` (chuyển 2 lần) — cần hoàn tay.
   - `transferAmount < bill.Total` → `Underpaid` — bill giữ `Pending`.
   - Ngược lại → `Matched` (dư thì `Overpaid`, vẫn coi là đã trả).
5. Matched/Overpaid:
   - `bill.MarkPaid(PaymentMethod.VietQr, amount, now)`
   - Xếp chỗ (mục 7).
   - Ghi `LedgerEntry` Thu, Category `Bán chỗ ngồi` (hoặc theo loại bill), Method `VietQr`.
6. Commit. Sau commit mới đẩy SignalR.
7. Unmatched/Underpaid/Duplicate: đẩy `PaymentNeedsAttention` cho POS chi nhánh (nếu xác định được), hoặc nhóm `managers`.

### 6.4 Phản hồi

Luôn `200 {"success": true}` khi đã xác thực xong, kể cả bỏ qua — trả lỗi chỉ khiến SePay retry vô ích.
Chỉ trả 401 khi sai chữ ký, 500 khi lỗi hạ tầng (DB chết) để SePay retry.
Phải xong trong < 30s.

## 7. Giữ chỗ & xếp chỗ

`Seat`: `Status` ∈ `Available | Held | Occupied | Maintenance`, `HeldForBillId`, `HeldUntil`.

- Tạo QR có `SeatId` → `Held` tới `now + HoldMinutes` (mặc định 10).
- Job `ReleaseExpiredSeatHolds` chạy mỗi phút: `Held && HeldUntil < now` → `Available`, bill vẫn `Pending`.
- Khi bill được trả (`SeatAllocator.Allocate`):
  1. Ghế đang `Held` cho đúng bill này → dùng ghế đó.
  2. Ghế khách chọn đã bị nhả nhưng vẫn `Available` → dùng lại.
  3. Không → chọn ghế mặc định cùng chi nhánh, cùng `SeatType`, theo `IDefaultSeatStrategy`
     (mặc định: ghế trống có `SortOrder` nhỏ nhất).
  4. Hết ghế → `bill.Flag(NeedsAttention, "NO_SEAT")`, POS nhận `PaymentNeedsAttention`.
- Cập nhật ghế dùng **optimistic concurrency** (`RowVersion`) để 2 webhook không giành 1 ghế.

## 8. Xác nhận thủ công

`GET /api/payments/transactions/attention` — danh sách giao dịch cần xử lý (`Unmatched`, `Underpaid`, `Duplicate` chưa đóng).

`POST /api/payments/transactions/{id}/assign` body `{ billId, note? }` — role `ShiftLead` hoặc `Manager`.
Dùng khi khách xoá mã khỏi nội dung, hoặc quản lý chấp nhận khoản chuyển thiếu. Chạy lại bước 5 với bill chỉ định
(bill coi như đã trả, Ledger ghi đúng số tiền thật nhận được). Trạng thái → `ManuallyAssigned`, ghi `ResolvedBy`, `ResolvedAt`.

`POST /api/payments/transactions/{id}/dismiss` body `{ note }` — role `Manager`. Đóng giao dịch mà không gắn bill
(ví dụ đã hoàn tiền tay cho khách chuyển 2 lần). Bắt buộc ghi lý do.

`POST /api/payments/bills/{id}/cash` — nhân viên thu tiền mặt. Ghi Ledger Method `Cash`, `ShiftId` ca đang mở.

## 9. Sổ cái thu chi (LedgerEntries)

Append-only. **Không UPDATE, không DELETE.** Sai → tạo dòng đảo có `ReversalOfId`.

Quy ước dòng đảo: **cùng** `Direction` và `Category` với dòng gốc, `Amount` vẫn dương, nhưng khi cộng tổng thì **trừ**
(`SignedAmount = -Amount`). Nhờ vậy đảo một khoản thu làm giảm đúng tổng thu của danh mục đó, không biến thành khoản chi.
Mỗi dòng chỉ đảo được một lần (unique index có filter trên `ReversalOfId`), không đảo dòng đảo.

| Cột | Kiểu | Ghi chú |
| --- | --- | --- |
| Id | bigint PK | |
| BranchId | int | |
| OccurredAt | datetime2 (UTC) | thời điểm tiền vào/ra thật |
| Direction | tinyint | 1 Thu, 2 Chi |
| Amount | bigint | luôn > 0 |
| Category | nvarchar(50) | `SeatSale`, `DrinkExtra`, `Ingredients`, `Payroll`, `Utilities`, `Refund`, `Other` |
| Method | tinyint | 1 VietQr, 2 Cash, 3 ManualTransfer |
| BillId | int NULL | |
| PaymentTransactionId | bigint NULL | |
| ShiftId | int NULL | |
| CreatedBy | int NULL | NULL = hệ thống |
| Note | nvarchar(255) NULL | |
| ReversalOfId | bigint NULL | dòng bị đảo |

API:
- `GET /api/ledger?branchId&from&to&direction&category&page` — lịch sử thu chi.
- `POST /api/ledger/expenses` — Manager nhập khoản chi.
- `POST /api/ledger/{id}/reverse` body `{ reason }` — Manager đảo bút toán.
- `GET /api/ledger/summary?branchId&period=week|month|quarter|year&from&to` — tổng thu, tổng chi, lãi theo kỳ + theo category.

Báo cáo **chỉ** đọc từ `LedgerEntries` (hoặc bảng tổng hợp `DailyLedgerSummaries` do job đêm tạo), không cộng từ `Bills`.

## 10. Đối soát

Job đêm `ReconcileVietQr` (Hangfire, 00:30 giờ VN):
- Tổng Ledger `Method=VietQr, Direction=Thu` của ngày hôm trước
  vs tổng `PaymentTransactions.TransferAmount` `MatchStatus ∈ {Matched, Overpaid}` cùng ngày.
- Lệch → ghi `ReconciliationIssues` + thông báo Manager.
- Liệt kê giao dịch `Unmatched/Underpaid/Duplicate` chưa xử lý.

## 11. Sự kiện realtime (SignalR, hub `/hubs/pos`)

Group: `branch-{branchId}`, `managers`.

| Event | Payload |
| --- | --- |
| `PaymentReceived` | `{ billId, amount, method, paidAt }` |
| `SeatAssigned` | `{ billId, seatId, seatCode, isDefault }` |
| `PaymentNeedsAttention` | `{ transactionId, billId?, reason, amount }` |
| `SeatHoldExpired` | `{ billId, seatId }` |

## 12. Cấu hình

```json
"Payment": {
  "Bank": "MBBank",
  "AccountNumber": "0123456789",
  "AccountHolder": "NGUYEN VAN A",
  "CodePrefix": "CR",
  "DescriptionPrefix": "",
  "WebhookSecret": "",          // user-secrets / AWS Secrets Manager — KHÔNG commit
  "TimestampToleranceSeconds": 300,
  "SeatHoldMinutes": 10,
  "QrTemplate": "compact"
}
```

## 13. Bảo mật

- HMAC + chống replay + chống trùng (mục 6).
- Kiểm tra `accountNumber` đúng tài khoản cấu hình.
- Không log full payload ra CloudWatch ở level Info (có tên người chuyển). Log ở Debug.
- Secret không nằm trong repo, `docker-compose` đọc từ `.env` (gitignore).
- Tài khoản ngân hàng demo tách riêng khỏi tài khoản cá nhân chính.

## 14. Kiểm thử

Unit:
- `SepaySignatureVerifier`: đúng chữ ký, sai chữ ký, timestamp quá hạn, header thiếu.
- `PaymentCode`: `CR1024`/`cr1024` → 1024; `CR`, `CRabc`, `CR0`, null → không hợp lệ; tìm trong nội dung chỉ khi có đúng 1 mã.
- `PaymentService`: matched, underpaid, overpaid, duplicate webhook, bill đã paid, sai account.
- `SeatAllocator`: ghế held đúng bill, ghế đã nhả còn trống, ghế bị người khác lấy → default, hết ghế.

Integration (Testcontainers SQL Server): 2 webhook cùng `id` gửi song song → chỉ 1 Ledger entry.

E2E thủ công:
1. SePay Test mode → mô phỏng giao dịch → POS kêu.
2. Live: bill 2.000đ, quét bằng Techcombank → POS kêu, ghế chuyển Occupied, Ledger có dòng Thu.

## 15. Definition of Done

- [ ] Tất cả unit test xanh.
- [ ] Webhook trả đúng `{"success": true}` trong < 1s với DB local.
- [ ] Gửi lại cùng webhook 3 lần → 1 Ledger entry.
- [ ] Hết hạn giữ chỗ → ghế về Available, POS nhận `SeatHoldExpired`.
- [ ] Báo cáo tháng khớp tổng Ledger.
- [ ] Không có secret trong git.
