using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using ChonRieng.Domain.Entities;

namespace ChonRieng.Api.Features.Scheduling;

/// <summary>Nhân viên trong phạm vi chi nhánh.</summary>
public sealed record EmployeeView(
    Guid EmployeeId,
    Guid UserId,
    string FullName,
    string Email,
    string? PhoneNumber,
    IReadOnlyList<string> Roles,
    decimal HourlyRate,
    decimal NightAllowance,
    IReadOnlyList<Guid> BranchIds,
    Guid? HomeBranchId,
    [property: Description("Tài khoản đang bị khóa.")] bool Locked);

public sealed record UpdateEmployeeRequest(
    [property: Range(0, 10_000_000), Description("Lương giờ (VND).")] decimal HourlyRate,
    [property: Range(0, 10_000_000), Description("Phụ cấp mỗi giờ làm đêm 22:00–06:00 (VND).")] decimal NightAllowance);

public sealed record UpdateEmployeeBranchesRequest(
    [property: Required, MinLength(1)] IReadOnlyList<Guid> BranchIds,
    [property: Description("Chi nhánh chính; phải nằm trong BranchIds.")] Guid HomeBranchId);

public sealed record CreateShiftRequestRequest(
    ShiftRequestType Type,
    Guid ShiftId,
    [property: Description("Bắt buộc với Swap: người nhận ca thay.")] Guid? TargetEmployeeId,
    [property: Required, MaxLength(1000)] string Reason);

public sealed record ReviewRequest([property: MaxLength(500)] string? Note);

/// <summary>Đơn đổi ca / xin nghỉ / trả ca.</summary>
public sealed record ShiftRequestView(
    Guid Id,
    ShiftRequestType Type,
    string Status,
    Guid ShiftId,
    Guid BranchId,
    string BranchName,
    DateOnly Date,
    ShiftType ShiftType,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    Guid RequesterEmployeeId,
    string RequesterName,
    Guid? TargetEmployeeId,
    string? TargetName,
    string Reason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReviewedAt);

public sealed record AvailabilitySlot(
    bool IsRecurring,
    [property: Description("Ngày trong tuần khi lặp lại hằng tuần.")] DayOfWeek? DayOfWeek,
    [property: Description("Ngày cụ thể khi không lặp lại.")] DateOnly? SpecificDate,
    TimeOnly From,
    TimeOnly To);

public sealed record AssistantEmployee(
    Guid EmployeeId,
    string FullName,
    IReadOnlyList<AvailabilitySlot> Availability,
    IReadOnlyList<ShiftSlot> Busy,
    double HoursThisRange);
