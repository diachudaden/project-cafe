namespace ChonRieng.Domain.Payments;

/// <summary>
/// Hợp đồng mà entity <c>Seat</c> đang có phải implement để giữ chỗ trong lúc chờ thanh toán.
/// </summary>
/// <remarks>
/// INTEGRATE: thêm vào <c>Seat</c> 2 cột <c>HeldForBillId</c>, <c>HeldUntil</c>, một cột <c>RowVersion</c>
/// (<c>[Timestamp] byte[]</c>) và implement interface này. Gợi ý:
/// <code>
/// public bool IsAvailable(DateTime nowUtc) =>
///     Status == SeatStatus.Available ||
///     (Status == SeatStatus.Held &amp;&amp; HeldUntil &lt; nowUtc);
///
/// public bool IsHeldFor(int billId) => Status == SeatStatus.Held &amp;&amp; HeldForBillId == billId;
///
/// public void Hold(int billId, DateTime untilUtc)
/// {
///     Status = SeatStatus.Held; HeldForBillId = billId; HeldUntil = untilUtc;
/// }
///
/// public void ReleaseHold()
/// {
///     if (Status != SeatStatus.Held) return;
///     Status = SeatStatus.Available; HeldForBillId = null; HeldUntil = null;
/// }
/// </code>
/// Việc chuyển ghế sang <c>Occupied</c> do <c>Session.Start</c> làm như cũ.
/// </remarks>
public interface IHoldableSeat
{
    int Id { get; }
    int BranchId { get; }

    /// <summary>Mã hiển thị, ví dụ "A12".</summary>
    string Code { get; }

    string SeatType { get; }

    /// <summary>Thứ tự ưu tiên khi hệ thống tự chọn (nhỏ hơn = ưu tiên hơn).</summary>
    int SortOrder { get; }

    int? HeldForBillId { get; }
    DateTime? HeldUntil { get; }

    /// <summary>Trống, hoặc đang giữ nhưng đã quá hạn.</summary>
    bool IsAvailable(DateTime nowUtc);

    /// <summary>Đang được giữ cho đúng bill này (kể cả đã quá hạn nhưng job chưa nhả).</summary>
    bool IsHeldFor(int billId);

    void Hold(int billId, DateTime untilUtc);

    void ReleaseHold();
}
