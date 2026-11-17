# Hướng dẫn kiểm thử VietQR + SePay Test mode

Tài liệu này hướng dẫn kiểm thử webhook bằng dữ liệu tổng hợp. Script chỉ tạo chữ ký trong bộ nhớ từ biến môi trường; không in hoặc lưu webhook secret. Đây là kiểm thử API tổng hợp, **không thay thế việc xác nhận webhook thật từ SePay hoặc môi trường triển khai**.

## 1. Cấu hình môi trường an toàn

Đặt các giá trị dưới đây trong secret manager hoặc file môi trường bị loại khỏi Git, trên đúng máy chạy API:

| Biến môi trường | Mục đích |
| --- | --- |
| `Payment__Bank` | Mã ngân hàng dùng tạo QR |
| `Payment__AccountNumber` | Tài khoản nhận tiền, phải khớp cấu hình SePay |
| `Payment__AccountHolder` | Tên chủ tài khoản hiển thị trên QR |
| `Payment__CodePrefix` | Tiền tố mã thanh toán, ví dụ `TT` |
| `Payment__QrImageBaseUrl` | URL dịch vụ tạo ảnh QR |
| `Payment__HoldMinutes` | Số phút giữ chỗ khi chờ thanh toán |
| `Payment__WebhookSecret` | Secret HMAC mới do SePay cấp; chỉ cấu hình qua secret manager hoặc môi trường |
| `Payment__TimestampToleranceSeconds` | Sai lệch timestamp cho phép, mặc định nên ngắn (ví dụ 300 giây) |

Secret từng được chia sẻ qua tài liệu/chat phải được **thu hồi và cấp lại trước khi dùng**. Không đưa secret vào source, task, shell history, URL, log hoặc ticket. Không chạy script với `set -x`.

## 2. Áp dụng migration đúng cơ sở dữ liệu

Migration `20261108085322_BillTenderTotals` bổ sung tổng tiền mặt/VietQR trên bill và backfill bill cũ theo `PaymentMethod`. Migration phải chạy có chủ đích tới đúng SQL Server của môi trường kiểm thử; API không tự migrate khi khởi động.

Trước tiên nạp connection string của **database kiểm thử** vào môi trường theo quy trình triển khai hiện có. Sau đó kiểm tra migration đích:

```sh
dotnet ef migrations list \
  --project backend/src/ChonRieng.Infrastructure \
  --startup-project backend/src/ChonRieng.Api
```

Chỉ khi xác nhận connection string trỏ đúng database kiểm thử mới áp dụng:

```sh
dotnet ef database update \
  --project backend/src/ChonRieng.Infrastructure \
  --startup-project backend/src/ChonRieng.Api
```

Không dùng database production cho bước thử này. Có thể kiểm tra migration đã áp dụng và cột tồn tại bằng:

```sql
SELECT [MigrationId]
FROM [__EFMigrationsHistory]
WHERE [MigrationId] = N'20261108085322_BillTenderTotals';

SELECT [name]
FROM sys.columns
WHERE [object_id] = OBJECT_ID(N'dbo.Bills')
  AND [name] IN (N'CashPaidTotal', N'VietQrPaidTotal');
```

Kết quả mong đợi: một dòng migration và hai cột.

## 3. Kiểm thử webhook tổng hợp

1. Khởi động API đã cấu hình với secret mới và database kiểm thử đã migrate.
2. Tạo yêu cầu thanh toán check-in từ POS, lấy `code` và `amount` do API trả về. Không tự sửa số tiền.
3. Đặt các biến môi trường cần thiết cho script trong terminal riêng:
   - `Payment__WebhookSecret`
   - `Payment__AccountNumber` (cùng giá trị với API)
   - `PAYMENT_CODE` (mã yêu cầu đang Pending)
   - `PAYMENT_AMOUNT` (đúng số tiền yêu cầu)
   - Tùy chọn `SEPAY_WEBHOOK_URL`, mặc định `http://localhost:5000/api/hooks/sepay-payment`
   - Tùy chọn `SEPAY_TRANSACTION_ID`; đặt cùng một ID để giả lập retry của cùng giao dịch
4. Chạy:

```sh
python3 scripts/sepay-webhook-test.py
```

Kết quả HTTP 200 nghĩa là sự kiện đã được xác thực và chấp nhận/xử lý. **HTTP 200 không đảm bảo bill đã được thanh toán**: kiểm tra trạng thái yêu cầu trong `GET /api/payments/{code}` và trong POS. Với khớp chính xác, mong đợi `Paid`, một session active và một bill VietQR. Gửi lại cùng `SEPAY_TRANSACTION_ID` phải không tạo bill/session thứ hai.

Để thử sai chữ ký, dùng request tổng hợp với chữ ký không hợp lệ và mong đợi HTTP 401. Không chỉnh trực tiếp database để tạo bằng chứng thành công.

## 4. Kiểm thử SePay thật và giới hạn hiện tại

Script trên chỉ kiểm tra API/HMAC với payload tổng hợp; nó không chứng minh SePay Test mode thực sự gửi đúng header/payload, cấu hình HTTPS/reverse proxy, hay callback tới môi trường đã triển khai. Các bước đó cần được chạy cùng tài khoản Test mode và endpoint công khai đã cấu hình. Chỉ ghi nhận hoàn tất sau khi xác minh:

- SePay gọi `POST /api/hooks/sepay-payment` qua HTTPS và nhận phản hồi dự kiến.
- Một check-in và một gia hạn được thanh toán Test mode tạo đúng bill/session/extension.
- Retry không tạo giao dịch bán hàng trùng.
- Từ chối chữ ký sai/stale và phân biệt giao dịch được ghi nhận với giao dịch đã settle.

Hiện chưa có bằng chứng truy cập môi trường triển khai hoặc callback Test mode thực tế; không coi các bước này là đã qua chỉ dựa trên unit tests.
