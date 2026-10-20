# Prompt khởi động cho Claude Code / Cursor

Copy nguyên khối dưới đây vào AI tool sau khi đã chép module vào repo (xem README.md).

---

Mình vừa thêm module thanh toán vào repo. Spec ở `docs/payments/SPEC.md`, code khung đã có sẵn trong các thư mục `Payments/` của từng tầng backend và `frontend/apps/pos/src/features/payments/`.

Việc cần làm, theo thứ tự, mỗi bước xong thì build + chạy test rồi mới sang bước sau:

1. Đọc `docs/payments/SPEC.md` và skill `chonrieng-payments`.
2. Tìm các chỗ có comment `// INTEGRATE:` trong code module. Mỗi chỗ là một điểm phải nối với entity/DbContext/hub đang có của dự án (Bill, Seat, Session, Shift, AppDbContext, PosHub). Nối chúng lại, **không đổi tên public API của module**.
3. Thêm migration EF Core cho `PaymentTransactions`, `LedgerEntries`, `ReconciliationIssues` và các cột giữ chỗ của `Seats` (`HeldForBillId`, `HeldUntil`, `RowVersion`).
4. Đăng ký `services.AddPayments(builder.Configuration)` và `app.MapPaymentEndpoints()` trong `Program.cs`. Đăng ký 2 job Hangfire: `ReleaseExpiredSeatHolds` mỗi phút, `ReconcileVietQr` 00:30 giờ VN.
5. Chạy `dotnet test --filter "FullyQualifiedName~Payments"`. Sửa tới khi xanh.
6. Frontend: gắn `PaymentQrDialog` vào luồng check-in của POS, gọi `usePaymentEvents(connection)` ở layout POS. Typecheck `pnpm -r tsc --noEmit`.
7. Chạy `node tools/payments/send-fake-webhook.mjs --code CR<billId> --amount <tiền>` với một bill thật trong DB local, xác nhận: bill Paid, ghế Occupied, có 1 dòng LedgerEntries, POS kêu.
8. Tự kiểm theo "Definition of Done" trong SPEC và báo lại cái nào chưa đạt.

Không tự ý đổi luật trong mục "Luật không được phá" của skill. Nếu thấy spec mâu thuẫn với code hiện có của dự án thì hỏi mình trước.
