namespace ChonRieng.Domain.Payments;

/// <summary>Một vấn đề phát hiện khi đối soát cuối ngày.</summary>
public sealed class ReconciliationIssue
{
    private ReconciliationIssue() { } // EF

    public long Id { get; private set; }

    /// <summary>Ngày đối soát theo giờ Việt Nam.</summary>
    public DateOnly BusinessDate { get; private set; }

    public ReconciliationIssueKind Kind { get; private set; }
    public long? PaymentTransactionId { get; private set; }

    /// <summary>Tổng theo sổ thu chi (VietQR).</summary>
    public long LedgerTotal { get; private set; }

    /// <summary>Tổng theo giao dịch SePay đã khớp.</summary>
    public long BankTotal { get; private set; }

    public string Message { get; private set; } = "";
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ResolvedAtUtc { get; private set; }
    public int? ResolvedBy { get; private set; }

    public static ReconciliationIssue TotalMismatch(DateOnly date, long ledgerTotal, long bankTotal, DateTime nowUtc) => new()
    {
        BusinessDate = date,
        Kind = ReconciliationIssueKind.TotalMismatch,
        LedgerTotal = ledgerTotal,
        BankTotal = bankTotal,
        Message = $"Sổ thu chi VietQR {ledgerTotal:N0}đ khác tiền ngân hàng đã khớp {bankTotal:N0}đ.",
        CreatedAtUtc = nowUtc,
    };

    public static ReconciliationIssue Unresolved(DateOnly date, PaymentTransaction tx, DateTime nowUtc) => new()
    {
        BusinessDate = date,
        Kind = ReconciliationIssueKind.UnresolvedTransaction,
        PaymentTransactionId = tx.SepayId,
        BankTotal = tx.Amount,
        Message = $"Giao dịch {tx.SepayId} ({tx.Amount:N0}đ, {tx.MatchStatus}) chưa được xử lý.",
        CreatedAtUtc = nowUtc,
    };

    public void Resolve(int by, DateTime nowUtc)
    {
        ResolvedBy = by;
        ResolvedAtUtc = nowUtc;
    }
}
