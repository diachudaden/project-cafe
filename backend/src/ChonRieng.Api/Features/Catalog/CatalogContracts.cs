using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using ChonRieng.Domain.Entities;

namespace ChonRieng.Api.Features.Catalog;

public enum SeatLiveState
{
    Available,
    Occupied,
    Held,
    EndingSoon,
    Overdue,
    Maintenance,
}

/// <summary>Số chỗ theo loại chỗ ngồi.</summary>
public sealed record SeatTypeAvailability(
    [property: Description("Loại chỗ: Single, Group, Nap.")] SeatType Type,
    [property: Description("Tổng số chỗ.")] int Total,
    [property: Description("Số chỗ đang trống.")] int Free,
    [property: Description("Số chỗ đang bảo trì.")] int Maintenance);

/// <summary>Chi nhánh đang hoạt động kèm số chỗ trống.</summary>
public sealed record PublicBranchResponse(
    Guid Id,
    string Name,
    string Address,
    decimal? Latitude,
    decimal? Longitude,
    bool Is24h,
    TimeOnly OpenTime,
    TimeOnly CloseTime,
    string? Phone,
    [property: Description("Chi nhánh có đang mở cửa theo giờ Việt Nam hay không.")] bool OpenNow,
    [property: Description("Khoảng cách (km) nếu gửi lat/lng.")] double? DistanceKm,
    IReadOnlyList<SeatTypeAvailability> Availability);

/// <summary>Bảng giá đang áp dụng.</summary>
public sealed record PricePlanView(
    Guid Id,
    Guid? BranchId,
    string Name,
    int DurationMinutes,
    decimal Price,
    decimal ExtendPrice,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidTo,
    [property: Description("Đang có hiệu lực tại thời điểm hiện tại.")] bool IsActive);

/// <summary>Lượt ngồi đang diễn ra tại một chỗ.</summary>
public sealed record SeatSessionInfo(
    Guid SessionId,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    [property: Description("Số phút còn lại; âm nếu quá giờ.")] int MinutesLeft,
    string? CustomerName,
    string BillCode,
    decimal BillTotal,
    [property: Description("Thời điểm hệ thống sẽ tự trả chỗ nếu không gia hạn (EndAt + thời gian ân hạn); null khi tắt tính năng.")] DateTimeOffset? AutoReleaseAt = null);

public sealed record FloorSeat(Guid Id, string Code, SeatType Type, SeatLiveState State, SeatSessionInfo? Session);

public sealed record FloorZone(Guid Id, string Name, IReadOnlyList<FloorSeat> Seats);

/// <summary>Sơ đồ chỗ ngồi realtime của chi nhánh.</summary>
public sealed record FloorResponse(Guid BranchId, string BranchName, IReadOnlyList<FloorZone> Zones);

public sealed record ZoneRequest([property: Required, MaxLength(100), Description("Tên khu vực.")] string Name);

public sealed record SeatRequest(
    [property: Required, MaxLength(32), Description("Mã chỗ, duy nhất trong khu vực.")] string Code,
    [property: Description("Loại chỗ: Single, Group, Nap.")] SeatType Type,
    [property: Description("Chuyển chỗ sang khu vực khác (tùy chọn).")] Guid? ZoneId = null);

public sealed record MaintenanceRequest([property: Description("true = đưa vào bảo trì; false = mở lại.")] bool Maintenance);

public sealed record PricePlanRequest(
    [property: Description("Chi nhánh áp dụng; null = toàn chuỗi (chỉ Admin).")] Guid? BranchId,
    [property: Required, MaxLength(150)] string Name,
    [property: Range(15, 1440), Description("Thời lượng một lượt (phút).")] int DurationMinutes,
    [property: Range(0, 100_000_000)] decimal Price,
    [property: Range(0, 100_000_000), Description("Giá mỗi lần gia hạn thêm một lượt.")] decimal ExtendPrice,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidTo);

public sealed record ZoneResponse(Guid Id, Guid BranchId, string Name);

public sealed record SeatResponse(Guid Id, Guid ZoneId, string Code, SeatType Type, SeatStatus Status);
