namespace ChonRieng.Domain.Payments;

/// <summary>
/// Log thô của một giao dịch tiền vào do SePay báo. Khoá chính là id giao dịch của SePay,
/// nên cùng một giao dịch gửi lại bao nhiêu lần cũng chỉ có một dòng (chống trùng ở tầng DB).
/// </summary>
public sealed class PaymentTransaction
{
    private PaymentTransaction() { } // EF

    /// <summary>Id giao dịch trên SePay. Không đổi qua các lần retry/replay.</summary>
    public long SepayId { get; private set; }

    public string Gateway { get; private set; } = "";
    public string AccountNumber { get; private set; } = "";
    public string? SubAccount { get; private set; }
    public string? Code { get; private set; }
    public string Content { get; private set; } = "";
    public string? Description { get; private set; }
    public string? ReferenceCode { get; private set; }

    /// <summary>Số tiền nhận, VND.</summary>
    public long Amount { get; private set; }

    /// <summary>Thời điểm ngân hàng ghi nhận giao dịch (UTC).</summary>
    public DateTime TransactionAtUtc { get; private set; }

    /// <summary>Thời điểm server nhận webhook (UTC).</summary>
    public DateTime ReceivedAtUtc { get; private set; }

    public PaymentMatchStatus MatchStatus { get; private set; }
    public int? BillId { get; private set; }
    public string? Note { get; private set; }

    public int? ResolvedBy { get; private set; }
    public DateTime? ResolvedAtUtc { get; private set; }

    /// <summary>Payload gốc để đối soát. Có tên người chuyển — không log ra ngoài ở mức Info.</summary>
    public string RawPayload { get; private set; } = "";

    public bool NeedsAttention =>
        (MatchStatus is PaymentMatchStatus.Unmatched or PaymentMatchStatus.Underpaid or PaymentMatchStatus.Duplicate)
        && ResolvedAtUtc is null;

    public static PaymentTransaction Receive(
        long sepayId,
        string gateway,
        string accountNumber,
        string? subAccount,
        string? code,
        string content,
        string? description,
        string? referenceCode,
        long amount,
        DateTime transactionAtUtc,
        DateTime receivedAtUtc,
        string rawPayload)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sepayId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);

        return new PaymentTransaction
        {
            SepayId = sepayId,
            Gateway = gateway,
            AccountNumber = accountNumber,
            SubAccount = string.IsNullOrEmpty(subAccount) ? null : subAccount,
            Code = code,
            Content = content,
            Description = string.IsNullOrEmpty(description) ? null : description,
            ReferenceCode = string.IsNullOrEmpty(referenceCode) ? null : referenceCode,
            Amount = amount,
            TransactionAtUtc = transactionAtUtc,
            ReceivedAtUtc = receivedAtUtc,
            MatchStatus = PaymentMatchStatus.Unmatched,
            RawPayload = rawPayload,
        };
    }

    public void SetMatch(PaymentMatchStatus status, int? billId, string? note = null)
    {
        MatchStatus = status;
        BillId = billId;
        Note = note;
    }

    /// <summary>Nhân viên gán tay giao dịch này cho một bill (khách xoá mã, chuyển thiếu được chấp nhận...).</summary>
    public void ResolveManually(int billId, int resolvedBy, DateTime nowUtc, string? note)
    {
        if (!NeedsAttention)
            throw new InvalidOperationException("Giao dịch này không cần xử lý thủ công.");

        MatchStatus = PaymentMatchStatus.ManuallyAssigned;
        BillId = billId;
        ResolvedBy = resolvedBy;
        ResolvedAtUtc = nowUtc;
        Note = note;
    }

    /// <summary>Đánh dấu đã xử lý xong ngoài hệ thống (ví dụ đã hoàn tiền tay cho khách chuyển 2 lần).</summary>
    public void Dismiss(int resolvedBy, DateTime nowUtc, string note)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(note);
        if (!NeedsAttention)
            throw new InvalidOperationException("Giao dịch này không cần xử lý thủ công.");

        ResolvedBy = resolvedBy;
        ResolvedAtUtc = nowUtc;
        Note = note;
    }
}
