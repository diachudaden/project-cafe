using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using ChonRieng.Domain.Entities;

namespace ChonRieng.Api.Features.Finance;

public sealed record ClosingSummary(
    Guid Id,
    ClosingStatus Status,
    decimal ExpectedCash,
    decimal CountedCash,
    decimal Difference,
    string? Note,
    IReadOnlyDictionary<string, int> Denominations,
    string ClosedByName,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? ReviewedAt,
    string? ReviewNote);

/// <summary>Số liệu đối chiếu trước khi kết ca.</summary>
public sealed record ClosingPreview(
    Guid ShiftId,
    Guid BranchId,
    string BranchName,
    ShiftType ShiftType,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    [property: Description("Tiền mặt dự kiến = tổng bill thanh toán tiền mặt của ca.")] decimal ExpectedCash,
    [property: Description("Tổng bill VietQR (giả lập), chỉ để tham khảo.")] decimal VietQrTotal,
    int BillCount,
    [property: Description("Số khách vẫn đang ngồi tại chi nhánh.")] int ActiveSessions,
    ClosingSummary? Closing);

public sealed record SubmitClosingRequest(
    [property: Required, Description("Số tờ theo mệnh giá VND, ví dụ { \"500000\": 2, \"10000\": 5 }.")] IReadOnlyDictionary<string, int> Denominations,
    [property: MaxLength(1000), Description("Bắt buộc khi có chênh lệch.")] string? Note,
    [property: Description("false = lưu nháp; true = nộp để quản lý duyệt và rời ca.")] bool Submit);

public sealed record ReviewClosingRequest(bool Approve, [property: MaxLength(1000)] string? Note);

/// <summary>Biên bản kết ca.</summary>
public sealed record ClosingListItem(
    Guid Id,
    Guid ShiftId,
    DateOnly Date,
    ShiftType ShiftType,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    ClosingSummary Closing);

public sealed record CreateExpenseRequest(
    Guid BranchId,
    [property: Required, MaxLength(80)] string Category,
    [property: Range(1, 1_000_000_000)] decimal Amount,
    DateTimeOffset Date,
    [property: MaxLength(1000)] string? Note);

public sealed record ReverseExpenseRequest([property: Required, MaxLength(500)] string Reason);

/// <summary>Bút toán chi. Sổ chỉ thêm; sai thì ghi bút toán đảo (số âm).</summary>
public sealed record ExpenseView(
    Guid Id,
    Guid BranchId,
    string Category,
    decimal Amount,
    DateTimeOffset Date,
    string? Note,
    string? CreatedByName,
    Guid? ReversesExpenseId,
    bool IsReversed);

public sealed record GeneratePayrollRequest(Guid BranchId, [property: Description("Ngày đầu tháng, ví dụ 2026-10-01.")] DateOnly Month);

public sealed record AdjustPayrollRequest([property: Range(0, 1_000_000_000)] decimal Bonus, [property: Range(0, 1_000_000_000)] decimal Deduction);

/// <summary>Bảng lương tháng của một nhân viên.</summary>
public sealed record PayrollView(
    Guid Id,
    Guid EmployeeId,
    string EmployeeName,
    DateOnly Month,
    decimal TotalHours,
    decimal BaseAmount,
    decimal Allowance,
    decimal Bonus,
    decimal Deduction,
    decimal NetAmount,
    string Status);

public sealed record PayrollLine(
    DateOnly Date,
    string BranchName,
    ShiftType ShiftType,
    DateTimeOffset CheckInAt,
    DateTimeOffset CheckOutAt,
    decimal Hours,
    decimal NightHours,
    bool Approved,
    [property: Description("Tiền ca = giờ × lương giờ + giờ đêm × phụ cấp (chỉ tính khi đã duyệt).")] decimal Amount);

public sealed record MyPayrollResponse(
    DateOnly Month,
    decimal HourlyRate,
    decimal NightAllowance,
    [property: Description("Tạm tính từ chấm công đã duyệt.")] decimal ProvisionalNet,
    PayrollView? Payroll,
    IReadOnlyList<PayrollLine> Lines);
