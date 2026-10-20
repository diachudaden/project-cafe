using ChonRieng.Application.Payments.Sepay;
using ChonRieng.Domain.Payments;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChonRieng.Application.Payments;

public enum WebhookOutcome
{
    Applied,            // bill đã Paid, đã xếp chỗ, đã ghi sổ
    NeedsAttention,     // đã lưu giao dịch nhưng cần nhân viên xử lý
    AlreadyProcessed,   // webhook trùng
    IgnoredNotIncoming, // tiền ra
    IgnoredWrongAccount,
    InvalidPayload,
}

/// <summary>
/// Toàn bộ nghiệp vụ thanh toán: tạo QR + giữ chỗ, xử lý webhook, thu tiền mặt, xử lý thủ công.
/// </summary>
public sealed class PaymentService(
    IPaymentStore store,
    IEnumerable<IPaymentProvider> providers,
    SeatAllocator seatAllocator,
    IPaymentNotifier notifier,
    IOptions<PaymentOptions> options,
    TimeProvider clock,
    ILogger<PaymentService> logger)
{
    private const int MaxAttempts = 3;
    private PaymentOptions Opt => options.Value;
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    // ------------------------------------------------------------------ Tạo QR

    public async Task<PayResult<PaymentQrDto>> CreateQrAsync(int billId, CancellationToken ct)
    {
        var provider = providers.Single(p => p.Method == PaymentMethod.VietQr);

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            store.ResetChanges();
            var now = Now;

            var bill = await store.GetBillAsync(billId, ct);
            if (bill is null) return PayErrors.BillNotFound;
            if (!bill.IsPayable) return PayErrors.BillNotPayable;

            var expiresAt = now.AddMinutes(Opt.SeatHoldMinutes);

            if (bill.RequiresSeat && bill.RequestedSeatId is int seatId)
            {
                var seat = await store.GetSeatAsync(seatId, ct);
                if (seat is null) return PayErrors.SeatNotFound;
                if (!seat.IsHeldFor(bill.Id) && !seat.IsAvailable(now)) return PayErrors.SeatTaken;

                seat.Hold(bill.Id, expiresAt); // gọi lại khi QR còn hạn = gia hạn giữ chỗ
            }

            var saved = await store.SaveChangesAsync(ct);
            if (saved == SaveOutcome.ConcurrencyConflict)
            {
                // Có người vừa giữ cùng ghế. Lần thử sau sẽ đọc lại trạng thái ghế và trả SEAT_TAKEN nếu đúng là mất.
                continue;
            }

            var request = provider.CreateRequest(bill, expiresAt);
            return PayResult<PaymentQrDto>.Ok(new PaymentQrDto(
                bill.Id, request.Code, request.Amount, request.QrImageUrl, request.ExpiresAtUtc,
                Opt.Bank, MaskAccount(Opt.AccountNumber), Opt.AccountHolder));
        }

        return PayErrors.SeatTaken;
    }

    // ------------------------------------------------------------------ Webhook SePay

    /// <summary>
    /// Xử lý một webhook đã xác thực chữ ký. Không ném lỗi nghiệp vụ — chỉ ném khi hạ tầng lỗi
    /// (để endpoint trả 500 và SePay retry).
    /// </summary>
    public async Task<WebhookOutcome> HandleSepayAsync(SepayWebhookPayload p, string rawPayload, CancellationToken ct)
    {
        if (!p.IsIncoming) return WebhookOutcome.IgnoredNotIncoming;

        if (p.Id <= 0 || p.TransferAmount <= 0 || !VietnamTime.TryParseSepay(p.TransactionDate, out var txAtUtc))
        {
            logger.LogWarning("SePay webhook payload không hợp lệ, id={SepayId}", p.Id);
            return WebhookOutcome.InvalidPayload;
        }

        if (!string.Equals(p.AccountNumber?.Trim(), Opt.AccountNumber.Trim(), StringComparison.Ordinal))
        {
            logger.LogWarning("SePay webhook {SepayId} về tài khoản {Account} không phải tài khoản cấu hình", p.Id, MaskAccount(p.AccountNumber));
            return WebhookOutcome.IgnoredWrongAccount;
        }

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            store.ResetChanges();
            var now = Now;

            if (await store.TransactionExistsAsync(p.Id, ct)) return WebhookOutcome.AlreadyProcessed;

            var tx = PaymentTransaction.Receive(
                p.Id, p.Gateway, p.AccountNumber!, p.SubAccount, p.Code, p.Content ?? "", p.Description,
                p.ReferenceCode, p.TransferAmount, txAtUtc, now, rawPayload);
            store.Add(tx);

            var plan = await MatchAsync(tx, p, ct);
            ApplyResult? applied = null;

            if (plan.Bill is not null && plan.Status is PaymentMatchStatus.Matched or PaymentMatchStatus.Overpaid)
            {
                var shiftId = await store.GetCurrentShiftIdAsync(plan.Bill.BranchId, ct);
                applied = await ApplyPaymentAsync(plan.Bill, PaymentMethod.VietQr, tx.Amount, txAtUtc, now,
                                                  tx.SepayId, shiftId, createdBy: null, ct);
            }

            tx.SetMatch(plan.Status, plan.Bill?.Id, plan.Note);

            switch (await store.SaveChangesAsync(ct))
            {
                case SaveOutcome.DuplicateKey:
                    return WebhookOutcome.AlreadyProcessed; // webhook song song cùng id đã lưu trước
                case SaveOutcome.ConcurrencyConflict:
                    logger.LogInformation("Xung đột ghế khi xử lý SePay {SepayId}, thử lại lần {Attempt}", p.Id, attempt + 1);
                    continue;
            }

            if (applied is not null)
            {
                await NotifyAppliedAsync(applied, ct);
                return WebhookOutcome.Applied;
            }

            await SafeNotifyAsync(() => notifier.NeedsAttentionAsync(plan.Bill?.BranchId,
                new PaymentNeedsAttentionEvent(tx.SepayId, plan.Bill?.Id, plan.Status.ToString(), tx.Amount), ct));
            return WebhookOutcome.NeedsAttention;
        }

        throw new InvalidOperationException($"Không xử lý được SePay {p.Id} sau {MaxAttempts} lần do xung đột ghế.");
    }

    private sealed record MatchPlan(PaymentMatchStatus Status, IPayableBill? Bill, string? Note);

    private async Task<MatchPlan> MatchAsync(PaymentTransaction tx, SepayWebhookPayload p, CancellationToken ct)
    {
        int billId;
        string? note = null;

        if (!PaymentCode.TryParse(p.Code, Opt.CodePrefix, out billId))
        {
            if (!PaymentCode.TryFindInContent(p.Content, Opt.CodePrefix, out billId))
                return new MatchPlan(PaymentMatchStatus.Unmatched, null, "Nội dung chuyển khoản không có mã thanh toán.");
            note = "Mã tìm thấy trong nội dung (SePay không tách được).";
        }

        var bill = await store.GetBillAsync(billId, ct);
        if (bill is null)
            return new MatchPlan(PaymentMatchStatus.Unmatched, null, $"Không có bill {billId}.");

        if (bill.IsPaid)
            return new MatchPlan(PaymentMatchStatus.Duplicate, bill, "Bill đã được thanh toán trước đó — cần hoàn tiền tay.");

        if (!bill.IsPayable)
            return new MatchPlan(PaymentMatchStatus.Unmatched, bill, "Bill đã huỷ hoặc không còn chờ thanh toán.");

        if (tx.Amount < bill.AmountDue)
            return new MatchPlan(PaymentMatchStatus.Underpaid, bill, $"Thiếu {bill.AmountDue - tx.Amount:N0}đ.");

        return tx.Amount > bill.AmountDue
            ? new MatchPlan(PaymentMatchStatus.Overpaid, bill, $"Dư {tx.Amount - bill.AmountDue:N0}đ.")
            : new MatchPlan(PaymentMatchStatus.Matched, bill, note);
    }

    // ------------------------------------------------------------------ Tiền mặt

    public async Task<PayResult<PaymentResultDto>> MarkPaidCashAsync(int billId, int staffId, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            store.ResetChanges();
            var now = Now;

            var bill = await store.GetBillAsync(billId, ct);
            if (bill is null) return PayErrors.BillNotFound;
            if (!bill.IsPayable) return PayErrors.BillNotPayable;

            var shiftId = await store.GetCurrentShiftIdAsync(bill.BranchId, ct);
            var applied = await ApplyPaymentAsync(bill, PaymentMethod.Cash, bill.AmountDue, now, now,
                                                  paymentTransactionId: null, shiftId, staffId, ct);

            var saved = await store.SaveChangesAsync(ct);
            if (saved == SaveOutcome.ConcurrencyConflict) continue;
            if (saved == SaveOutcome.DuplicateKey) return PayErrors.BillNotPayable;

            await NotifyAppliedAsync(applied, ct);
            return PayResult<PaymentResultDto>.Ok(applied.ToDto());
        }

        return PayErrors.Busy;
    }

    // ------------------------------------------------------------------ Xử lý thủ công

    public async Task<IReadOnlyList<PaymentTransactionDto>> ListNeedingAttentionAsync(CancellationToken ct) =>
        (await store.GetTransactionsNeedingAttentionAsync(null, null, ct)).Select(t => t.ToDto()).ToList();

    /// <summary>Gán một giao dịch chưa khớp (khách xoá mã, chuyển thiếu...) cho bill. Coi như bill đã trả đủ.</summary>
    public async Task<PayResult<PaymentResultDto>> AssignTransactionAsync(long sepayId, int billId, int staffId, string? note, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            store.ResetChanges();
            var now = Now;

            var tx = await store.GetTransactionAsync(sepayId, ct);
            if (tx is null) return PayErrors.TransactionNotFound;
            if (!tx.NeedsAttention) return PayErrors.TransactionResolved;

            var bill = await store.GetBillAsync(billId, ct);
            if (bill is null) return PayErrors.BillNotFound;
            if (!bill.IsPayable) return PayErrors.BillNotPayable;

            var shiftId = await store.GetCurrentShiftIdAsync(bill.BranchId, ct);
            var applied = await ApplyPaymentAsync(bill, PaymentMethod.VietQr, tx.Amount, tx.TransactionAtUtc, now,
                                                  tx.SepayId, shiftId, staffId, ct);
            tx.ResolveManually(bill.Id, staffId, now, note);

            var saved = await store.SaveChangesAsync(ct);
            if (saved == SaveOutcome.ConcurrencyConflict) continue;
            if (saved == SaveOutcome.DuplicateKey) return PayErrors.TransactionResolved;

            await NotifyAppliedAsync(applied, ct);
            return PayResult<PaymentResultDto>.Ok(applied.ToDto());
        }

        return PayErrors.Busy;
    }

    /// <summary>Đóng một giao dịch cần xử lý mà không gắn bill (ví dụ đã hoàn tiền tay cho khách chuyển 2 lần).</summary>
    public async Task<PayResult<PaymentTransactionDto>> DismissTransactionAsync(long sepayId, int staffId, string note, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(note))
            return PayError.Validation("NOTE_REQUIRED", "Cần ghi lý do.");

        var tx = await store.GetTransactionAsync(sepayId, ct);
        if (tx is null) return PayErrors.TransactionNotFound;
        if (!tx.NeedsAttention) return PayErrors.TransactionResolved;

        tx.Dismiss(staffId, Now, note);
        await store.SaveChangesAsync(ct);
        return PayResult<PaymentTransactionDto>.Ok(tx.ToDto());
    }

    // ------------------------------------------------------------------ Lõi

    private sealed record ApplyResult(IPayableBill Bill, long Amount, PaymentMethod Method, DateTime PaidAtUtc, SeatAllocation Seat)
    {
        public PaymentResultDto ToDto() => new(
            Bill.Id, Amount, Method, Seat.Seat?.Id, Seat.Seat?.Code, Seat.IsDefault, Seat.NoSeatAvailable);
    }

    /// <summary>Đánh dấu Paid + xếp chỗ + ghi sổ. Không lưu — người gọi SaveChanges một lần cho tất cả.</summary>
    private async Task<ApplyResult> ApplyPaymentAsync(
        IPayableBill bill, PaymentMethod method, long amount, DateTime paidAtUtc, DateTime nowUtc,
        long? paymentTransactionId, int? shiftId, int? createdBy, CancellationToken ct)
    {
        bill.MarkPaid(method, amount, paidAtUtc);

        var seat = await seatAllocator.AllocateAsync(bill, nowUtc, ct);

        store.Add(LedgerEntry.Income(
            branchId: bill.BranchId,
            occurredAtUtc: paidAtUtc,
            amount: amount,
            category: bill.LedgerCategory,
            method: method,
            createdAtUtc: nowUtc,
            billId: bill.Id,
            paymentTransactionId: paymentTransactionId,
            shiftId: shiftId,
            createdBy: createdBy));

        return new ApplyResult(bill, amount, method, paidAtUtc, seat);
    }

    private async Task NotifyAppliedAsync(ApplyResult r, CancellationToken ct)
    {
        var branchId = r.Bill.BranchId;
        await SafeNotifyAsync(() => notifier.PaymentReceivedAsync(branchId,
            new PaymentReceivedEvent(r.Bill.Id, r.Amount, r.Method, r.PaidAtUtc), ct));

        if (r.Seat.Seat is { } seat)
        {
            await SafeNotifyAsync(() => notifier.SeatAssignedAsync(branchId,
                new SeatAssignedEvent(r.Bill.Id, seat.Id, seat.Code, r.Seat.IsDefault), ct));
        }
        else if (r.Seat.NoSeatAvailable)
        {
            await SafeNotifyAsync(() => notifier.NeedsAttentionAsync(branchId,
                new PaymentNeedsAttentionEvent(null, r.Bill.Id, "NO_SEAT", r.Amount), ct));
        }
    }

    /// <summary>Đã commit rồi thì lỗi SignalR không được làm hỏng webhook (nếu không SePay sẽ retry vô ích).</summary>
    private async Task SafeNotifyAsync(Func<Task> send)
    {
        try { await send(); }
        catch (Exception ex) { logger.LogWarning(ex, "Gửi sự kiện thanh toán realtime thất bại"); }
    }

    private static string MaskAccount(string? account) =>
        string.IsNullOrEmpty(account) || account.Length <= 4 ? account ?? "" : new string('*', account.Length - 4) + account[^4..];
}
