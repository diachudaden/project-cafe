using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using ChonRieng.Api.Features.Common;
using ChonRieng.Domain.Entities;

namespace ChonRieng.Api.Features.Scheduling;

/// <summary>Trạng thái hiển thị của ca, suy ra từ ca, phân công, chấm công và kết ca.</summary>
public enum ShiftPhase
{
    Draft,
    Published,
    InProgress,
    Closed,
    Approved,
}

public sealed record AssignmentView(
    Guid Id,
    Guid EmployeeId,
    string EmployeeName,
    ShiftAssignmentStatus Status,
    DateTimeOffset? RespondedAt,
    string? DeclineReason,
    string? OverrideReason);

/// <summary>Ca làm kèm danh sách phân công.</summary>
public sealed record ShiftView(
    Guid Id,
    Guid BranchId,
    string BranchName,
    DateOnly Date,
    ShiftType Type,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    int RequiredStaff,
    [property: Description("Ca trống: nhân viên đủ điều kiện được tự nhận.")] bool IsOpen,
    ShiftStatus Status,
    ShiftPhase Phase,
    [property: Description("Gửi lại khi sửa ca để phát hiện xung đột cập nhật (base64).")] string RowVersion,
    int AcceptedCount,
    [property: Description("Ca chưa đủ số người tối thiểu.")] bool Understaffed,
    IReadOnlyList<AssignmentView> Assignments);

public sealed record CreateShiftRequest(
    Guid BranchId,
    DateOnly Date,
    ShiftType Type,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    [property: Range(1, 50)] int RequiredStaff,
    bool IsOpen);

public sealed record UpdateShiftRequest(
    DateOnly Date,
    ShiftType Type,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    [property: Range(1, 50)] int RequiredStaff,
    bool IsOpen,
    [property: Required, Description("RowVersion nhận được khi đọc ca.")] string RowVersion);

public sealed record AssignRequest(
    Guid EmployeeId,
    [property: MaxLength(500), Description("Lý do bỏ qua quy tắc có thể bỏ qua (ví dụ thiếu giờ nghỉ).")] string? OverrideReason);

public sealed record ValidateAssignmentRequest(Guid ShiftId, Guid EmployeeId);

public sealed record ValidationResultResponse(
    [property: Description("Có thể phân công (không vi phạm chặn, hoặc tất cả đều được bỏ qua kèm lý do).")] bool Allowed,
    [property: Description("true nếu chỉ còn vi phạm có thể bỏ qua kèm lý do.")] bool NeedsOverrideReason,
    IReadOnlyList<RuleViolation> Violations);

public sealed record PublishRequest(Guid BranchId, [property: Description("Ngày thứ Hai của tuần.")] DateOnly WeekStart);

public sealed record PublishResponse(int Published, int NotifiedEmployees);

public sealed record CopyWeekRequest(Guid BranchId, DateOnly SourceWeekStart, DateOnly TargetWeekStart, bool IncludeAssignments);

public sealed record CopyConflict(DateOnly Date, ShiftType Type, string EmployeeName, IReadOnlyList<RuleViolation> Violations);

public sealed record CopyWeekResponse(int Created, int Skipped, IReadOnlyList<CopyConflict> Conflicts);

public sealed record DeclineRequest([property: Required, MaxLength(500), Description("Lý do từ chối.")] string Reason);

public sealed record Colleague(Guid EmployeeId, string Name, ShiftAssignmentStatus Status);

/// <summary>Ca của tôi (nhân viên).</summary>
public sealed record MyShiftView(
    Guid AssignmentId,
    ShiftAssignmentStatus AssignmentStatus,
    Guid ShiftId,
    Guid BranchId,
    string BranchName,
    string BranchAddress,
    DateOnly Date,
    ShiftType Type,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    ShiftPhase Phase,
    DateTimeOffset? CheckInAt,
    DateTimeOffset? CheckOutAt,
    [property: Description("Nút 'Tham gia ca' mở từ 15 phút trước giờ bắt đầu.")] bool CanCheckIn,
    IReadOnlyList<Colleague> Colleagues);

public sealed record OpenShiftView(
    Guid ShiftId,
    Guid BranchId,
    string BranchName,
    DateOnly Date,
    ShiftType Type,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    int RequiredStaff,
    int AcceptedCount,
    [property: Description("Ca thuộc chi nhánh nhân viên đã được gán; ca ở chi nhánh khác vẫn nhận được và sẽ tự gán chi nhánh.")] bool IsMyBranch);

public sealed record CheckInRequest(Guid ShiftId);

/// <summary>Bản ghi chấm công.</summary>
public sealed record AttendanceView(
    Guid Id,
    Guid ShiftId,
    Guid EmployeeId,
    DateTimeOffset? CheckInAt,
    DateTimeOffset? CheckOutAt,
    [property: Description("Số phút đi trễ; > 10 phút được đánh dấu trễ.")] int LateMinutes,
    bool IsLate,
    bool Approved);

/// <summary>Dòng duyệt chấm công: giờ ca so với giờ thực tế.</summary>
public sealed record AttendanceReviewRow(
    Guid? AttendanceId,
    Guid ShiftId,
    DateOnly Date,
    ShiftType ShiftType,
    DateTimeOffset ShiftStart,
    DateTimeOffset ShiftEnd,
    Guid EmployeeId,
    string EmployeeName,
    DateTimeOffset? CheckInAt,
    DateTimeOffset? CheckOutAt,
    int LateMinutes,
    int EarlyLeaveMinutes,
    int OvertimeMinutes,
    [property: Description("Đã nhận ca nhưng không điểm danh.")] bool Missing,
    bool Approved);

public sealed record ApproveAttendanceRequest(bool Approved);

/// <summary>Ca đang làm của nhân viên (thanh trên cùng POS).</summary>
public sealed record CurrentShiftResponse(
    Guid ShiftId,
    Guid BranchId,
    string BranchName,
    ShiftType Type,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    DateTimeOffset CheckInAt,
    [property: Description("Tiền mặt dự kiến đang giữ trong ca (tổng bill tiền mặt).")] decimal CashHeld,
    int BillCount);
