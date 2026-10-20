namespace ChonRieng.Domain.Payments;

/// <summary>
/// Hợp đồng mà entity <c>Bill</c> đang có của dự án phải implement để module thanh toán dùng được.
/// Module không phụ thuộc trực tiếp vào class Bill, chỉ phụ thuộc vào interface này.
/// </summary>
/// <remarks>
/// INTEGRATE: cho <c>Bill</c> implement interface này. Gợi ý ánh xạ:
/// <code>
/// public sealed class Bill : IPayableBill
/// {
///     long IPayableBill.AmountDue => (long)Total;
///     bool IPayableBill.IsPayable => Status == BillStatus.Pending;
///     bool IPayableBill.IsPaid    => Status == BillStatus.Paid;
///     public void MarkPaid(PaymentMethod m, long received, DateTime atUtc)
///     {
///         if (Status != BillStatus.Pending) throw new InvalidOperationException("Bill không ở trạng thái chờ thanh toán.");
///         Status = BillStatus.Paid; PaymentMethod = m; AmountReceived = received; PaidAt = atUtc;
///     }
///     public void FlagNeedsAttention(string reason) { NeedsAttention = true; AttentionReason = reason; }
/// }
/// </code>
/// </remarks>
public interface IPayableBill
{
    int Id { get; }
    int BranchId { get; }

    /// <summary>Số tiền phải trả, VND.</summary>
    long AmountDue { get; }

    /// <summary>Bill đang chờ thanh toán (chưa trả, chưa huỷ).</summary>
    bool IsPayable { get; }

    bool IsPaid { get; }

    /// <summary>Bill này có cần xếp chỗ khi trả xong không (bill gọi thêm nước thì không).</summary>
    bool RequiresSeat { get; }

    /// <summary>Ghế khách chọn trên đơn, null nếu để hệ thống chọn.</summary>
    int? RequestedSeatId { get; }

    /// <summary>Loại ghế mong muốn (bàn đơn, bàn nhóm...) khi không chọn ghế cụ thể.</summary>
    string? RequestedSeatType { get; }

    /// <summary>Danh mục ghi vào sổ thu chi khi bill được trả.</summary>
    LedgerCategory LedgerCategory { get; }

    void MarkPaid(PaymentMethod method, long amountReceived, DateTime paidAtUtc);

    void FlagNeedsAttention(string reason);
}
