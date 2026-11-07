using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using ChonRieng.Domain.Entities;

namespace ChonRieng.Api.Features.Sales;

public sealed record CheckInSessionRequest(
    Guid SeatId,
    Guid PricePlanId,
    [property: Description("Cash hoặc VietQr (giả lập, không có cổng thanh toán thật).")] PaymentMethod PaymentMethod,
    [property: Description("Khách thành viên (tùy chọn). Nếu có, điểm được cộng tự động.")] Guid? CustomerId);

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
    [property: Description("Điểm khách được cộng trong thao tác này (nếu có).")] int PointsAwarded);

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

public sealed record MyActiveSession(Guid SessionId, string BranchName, string SeatCode, DateTimeOffset StartAt, DateTimeOffset EndAt, int MinutesLeft);
