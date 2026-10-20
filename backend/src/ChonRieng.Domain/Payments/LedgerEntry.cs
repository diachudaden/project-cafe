namespace ChonRieng.Domain.Payments;

/// <summary>
/// Một dòng trong sổ thu chi. Append-only: tạo ra rồi thì không sửa, không xoá.
/// Ghi sai thì tạo bút toán đảo bằng <see cref="Reverse"/>.
/// </summary>
/// <remarks>
/// Quy ước cộng tổng: dòng thường cộng <see cref="Amount"/>, dòng đảo (có <see cref="ReversalOfId"/>) trừ <see cref="Amount"/>,
/// cùng <see cref="Direction"/> và <see cref="Category"/> với dòng gốc. Nhờ vậy đảo một khoản thu sẽ làm giảm đúng
/// tổng thu của danh mục đó, không bị tính thành khoản chi.
/// </remarks>
public sealed class LedgerEntry
{
    private LedgerEntry() { } // EF

    public long Id { get; private set; }
    public int BranchId { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    public LedgerDirection Direction { get; private set; }

    /// <summary>Luôn dương, VND.</summary>
    public long Amount { get; private set; }

    public LedgerCategory Category { get; private set; }
    public PaymentMethod Method { get; private set; }

    public int? BillId { get; private set; }

    /// <summary>Id giao dịch SePay (khoá của <see cref="PaymentTransaction"/>), nếu thu qua VietQR.</summary>
    public long? PaymentTransactionId { get; private set; }

    public int? ShiftId { get; private set; }

    /// <summary>Người tạo. Null = hệ thống tự ghi (webhook, job).</summary>
    public int? CreatedBy { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public string? Note { get; private set; }

    /// <summary>Dòng này đảo dòng nào.</summary>
    public long? ReversalOfId { get; private set; }

    public bool IsReversal => ReversalOfId is not null;

    /// <summary>Giá trị có dấu dùng khi cộng tổng theo Direction/Category.</summary>
    public long SignedAmount => IsReversal ? -Amount : Amount;

    public static LedgerEntry Income(
        int branchId,
        DateTime occurredAtUtc,
        long amount,
        LedgerCategory category,
        PaymentMethod method,
        DateTime createdAtUtc,
        int? billId = null,
        long? paymentTransactionId = null,
        int? shiftId = null,
        int? createdBy = null,
        string? note = null)
        => Create(LedgerDirection.In, branchId, occurredAtUtc, amount, category, method, createdAtUtc,
                  billId, paymentTransactionId, shiftId, createdBy, note);

    public static LedgerEntry Expense(
        int branchId,
        DateTime occurredAtUtc,
        long amount,
        LedgerCategory category,
        PaymentMethod method,
        int createdBy,
        DateTime createdAtUtc,
        int? shiftId = null,
        string? note = null)
        => Create(LedgerDirection.Out, branchId, occurredAtUtc, amount, category, method, createdAtUtc,
                  billId: null, paymentTransactionId: null, shiftId, createdBy, note);

    /// <summary>Tạo bút toán đảo cho dòng này. Không được đảo một dòng đảo.</summary>
    public LedgerEntry Reverse(int reversedBy, DateTime nowUtc, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (IsReversal)
            throw new InvalidOperationException("Không thể đảo một bút toán đảo.");
        if (Id == 0)
            throw new InvalidOperationException("Chỉ đảo được dòng đã lưu.");

        var reversal = Create(Direction, BranchId, nowUtc, Amount, Category, Method, nowUtc,
                              BillId, PaymentTransactionId, shiftId: null, reversedBy, reason);
        reversal.ReversalOfId = Id;
        return reversal;
    }

    private static LedgerEntry Create(
        LedgerDirection direction,
        int branchId,
        DateTime occurredAtUtc,
        long amount,
        LedgerCategory category,
        PaymentMethod method,
        DateTime createdAtUtc,
        int? billId,
        long? paymentTransactionId,
        int? shiftId,
        int? createdBy,
        string? note)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(branchId);
        if (note is { Length: > 255 }) note = note[..255];

        return new LedgerEntry
        {
            BranchId = branchId,
            OccurredAtUtc = occurredAtUtc,
            Direction = direction,
            Amount = amount,
            Category = category,
            Method = method,
            BillId = billId,
            PaymentTransactionId = paymentTransactionId,
            ShiftId = shiftId,
            CreatedBy = createdBy,
            CreatedAtUtc = createdAtUtc,
            Note = note,
        };
    }
}
