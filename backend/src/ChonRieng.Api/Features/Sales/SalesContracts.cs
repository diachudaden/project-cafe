using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using ChonRieng.Domain.Entities;

namespace ChonRieng.Api.Features.Sales;

public sealed record CheckInSessionRequest(
    Guid SeatId,
    Guid PricePlanId,
    [property: Description("Cash hoặc VietQr (giả lập, không có cổng thanh toán thật).")] PaymentMethod PaymentMethod,
    [property: Description("Khách thành viên (tùy chọn). Nếu có, điểm được cộng tự động.")] Guid? CustomerId,
    [property: MaxLength(AddOnService.MaxLines), Description("Đồ thêm mua cùng lúc check-in (tùy chọn, tối đa 30 dòng, số lượng 1–99). Giá lấy từ thực đơn của chi nhánh tại máy chủ; tổng bill = giá gói + đồ thêm, điểm thành viên cộng một lần trên tổng.")] IReadOnlyList<AddOnLineRequest>? Items = null);

public sealed record SessionCustomer(Guid Id, string FullName, string Tier);

public sealed record SessionBill(
    Guid Id,
    [property: Description("Mã 8 ký tự in trên hóa đơn để khách tích điểm.")] string Code,
    decimal Total,
    PaymentMethod PaymentMethod,
    bool Claimed,
    DateTimeOffset CreatedAt);

/// <summary>Lượt ngồi.</summary>
public sealed record SessionView(
    Guid Id,
    Guid BranchId,
    Guid SeatId,
    string SeatCode,
    Guid PricePlanId,
    string PlanName,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    SessionStatus Status,
    [property: Description("Số phút còn lại; âm nếu quá giờ.")] int MinutesLeft,
    int ExtensionCount,
    SessionCustomer? Customer,
    SessionBill Bill,
    [property: Description("Điểm khách được cộng trong thao tác này (nếu có).")] int PointsAwarded,
    [property: Description("Đồ thêm (mì, trứng, bánh...) đã bán trên hóa đơn này.")] IReadOnlyList<BillItemView> Items,
    [property: Description("Lý do kết thúc: Manual (nhân viên trả chỗ) hoặc AutoReleased (hệ thống tự trả chỗ khi quá giờ). null khi đang diễn ra.")] SessionCloseReason? CloseReason = null);

public sealed record RecentClosedSession(
    Guid SessionId,
    string SeatCode,
    [property: Description("Giờ kết thúc đã trả tiền.")] DateTimeOffset EndAt,
    [property: Description("Thời điểm trả chỗ.")] DateTimeOffset ClosedAt,
    [property: Description("Manual = nhân viên trả chỗ; AutoReleased = hệ thống tự trả chỗ.")] SessionCloseReason? CloseReason,
    string? CustomerName,
    string BillCode,
    decimal BillTotal);

public sealed record CustomerLookupResult(Guid CustomerId, string FullName, string Tier, int Points, string? MemberCode);

public sealed record ClaimBillRequest([property: Required, StringLength(8, MinimumLength = 8), Description("Mã bill 8 ký tự.")] string Code);

public sealed record ClaimBillResponse(string Code, decimal Total, int PointsEarned, int Balance, string Tier);

public sealed record PointHistoryItem(Guid Id, int Points, string Reason, string? BillCode, DateTimeOffset CreatedAt);

/// <summary>Điểm và hạng thành viên của tôi.</summary>
public sealed record MyPointsResponse(
    int Points,
    string Tier,
    decimal TotalSpent,
    [property: Description("Hạng kế tiếp; null nếu đã là hạng cao nhất.")] string? NextTier,
    [property: Description("Số tiền cần chi thêm để lên hạng kế tiếp.")] decimal AmountToNextTier,
    string MemberCode,
    IReadOnlyList<PointHistoryItem> History);

public sealed record MyBill(string Code, string BranchName, decimal Total, DateTimeOffset CreatedAt, DateTimeOffset? ClaimedAt, int Points);

public sealed record MyBillExtension(DateTimeOffset CreatedAt, int AddedMinutes, decimal Price);

public sealed record MyBillPoints(int Points, string Reason, DateTimeOffset CreatedAt);

public sealed record MyBillDetail(
    string Code,
    string BranchName,
    string SeatCode,
    string PlanName,
    DateTimeOffset StartAt,
    [property: Description("Giờ kết thúc (đã gồm các lần gia hạn).")] DateTimeOffset EndAt,
    SessionStatus SessionStatus,
    [property: Description("Manual / AutoReleased khi lượt đã kết thúc.")] SessionCloseReason? CloseReason,
    IReadOnlyList<MyBillExtension> Extensions,
    [property: Description("Đồ thêm đã mua trên hóa đơn.")] IReadOnlyList<BillItemView> Items,
    decimal CashPaidTotal,
    decimal VietQrPaidTotal,
    decimal Total,
    PaymentMethod PaymentMethod,
    [property: Description("Các lần cộng điểm của hóa đơn này.")] IReadOnlyList<MyBillPoints> Points,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ClaimedAt);

public sealed record MyActiveSession(Guid SessionId, string BranchName, string SeatCode, DateTimeOffset StartAt, DateTimeOffset EndAt, int MinutesLeft);
