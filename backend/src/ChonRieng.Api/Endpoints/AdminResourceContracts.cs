using System.ComponentModel.DataAnnotations;
using ChonRieng.Domain.Entities;

namespace ChonRieng.Api.Endpoints;

/// <summary>Branch data returned by the administrator API.</summary>
public sealed record BranchResponse(Guid Id, string Name, string Address, decimal? Latitude, decimal? Longitude, bool Is24h, TimeOnly OpenTime, TimeOnly CloseTime, string? Phone, bool IsActive);
/// <summary>Payload for creating a branch.</summary>
public sealed record CreateBranchRequest([property: Required, MaxLength(150)] string Name, [property: Required, MaxLength(500)] string Address, decimal? Latitude, decimal? Longitude, bool Is24h, TimeOnly OpenTime, TimeOnly CloseTime, [property: MaxLength(32)] string? Phone, bool IsActive);
/// <summary>Payload for replacing a branch.</summary>
public sealed record UpdateBranchRequest([property: Required, MaxLength(150)] string Name, [property: Required, MaxLength(500)] string Address, decimal? Latitude, decimal? Longitude, bool Is24h, TimeOnly OpenTime, TimeOnly CloseTime, [property: MaxLength(32)] string? Phone, bool IsActive);

/// <summary>Zone data returned by the administrator API.</summary>
public sealed record ZoneResponse(Guid Id, Guid BranchId, string Name);
/// <summary>Payload for creating a zone.</summary>
public sealed record CreateZoneRequest(Guid BranchId, [property: Required, MaxLength(100)] string Name);
/// <summary>Payload for replacing a zone.</summary>
public sealed record UpdateZoneRequest(Guid BranchId, [property: Required, MaxLength(100)] string Name);

/// <summary>Seat data returned by the administrator API.</summary>
public sealed record SeatResponse(Guid Id, Guid ZoneId, string Code, SeatType Type, SeatStatus Status);
/// <summary>Payload for creating a seat.</summary>
public sealed record CreateSeatRequest(Guid ZoneId, [property: Required, MaxLength(32)] string Code, SeatType Type, SeatStatus Status);
/// <summary>Payload for replacing a seat.</summary>
public sealed record UpdateSeatRequest(Guid ZoneId, [property: Required, MaxLength(32)] string Code, SeatType Type, SeatStatus Status);

/// <summary>Price-plan data returned by the administrator API.</summary>
public sealed record PricePlanResponse(Guid Id, Guid? BranchId, string Name, int DurationMinutes, decimal Price, decimal ExtendPrice, DateTimeOffset? ValidFrom, DateTimeOffset? ValidTo);
/// <summary>Payload for creating a price plan.</summary>
public sealed record CreatePricePlanRequest(Guid? BranchId, [property: Required, MaxLength(150)] string Name, int DurationMinutes, decimal Price, decimal ExtendPrice, DateTimeOffset? ValidFrom, DateTimeOffset? ValidTo);
/// <summary>Payload for replacing a price plan.</summary>
public sealed record UpdatePricePlanRequest(Guid? BranchId, [property: Required, MaxLength(150)] string Name, int DurationMinutes, decimal Price, decimal ExtendPrice, DateTimeOffset? ValidFrom, DateTimeOffset? ValidTo);

/// <summary>Employee data returned by the administrator API. The PIN hash is never included.</summary>
public sealed record EmployeeResponse(Guid Id, Guid UserId, decimal HourlyRate, decimal NightAllowance);
/// <summary>Payload for creating an employee and setting an initial PIN.</summary>
public sealed record CreateEmployeeRequest(Guid UserId, decimal HourlyRate, decimal NightAllowance, [property: Required] string InitialPin);
/// <summary>Payload for replacing employee settings and optionally resetting the PIN.</summary>
public sealed record UpdateEmployeeRequest(decimal HourlyRate, decimal NightAllowance, string? NewPin);

/// <summary>Customer profile data returned by the administrator API.</summary>
public sealed record CustomerResponse(Guid Id, Guid UserId, int Points, string Tier, decimal TotalSpent);
/// <summary>Payload for creating a customer profile.</summary>
public sealed record CreateCustomerRequest(Guid UserId, int Points, [property: Required, MaxLength(64)] string Tier, decimal TotalSpent);
/// <summary>Payload for replacing a customer profile.</summary>
public sealed record UpdateCustomerRequest(Guid UserId, int Points, [property: Required, MaxLength(64)] string Tier, decimal TotalSpent);

/// <summary>Shift data returned by the administrator API.</summary>
public sealed record ShiftResponse(Guid Id, Guid BranchId, DateOnly Date, ShiftType Type, DateTimeOffset StartAt, DateTimeOffset EndAt, int RequiredStaff, bool IsOpen, ShiftStatus Status);
/// <summary>Payload for creating a shift.</summary>
public sealed record CreateShiftRequest(Guid BranchId, DateOnly Date, ShiftType Type, DateTimeOffset StartAt, DateTimeOffset EndAt, int RequiredStaff, bool IsOpen, ShiftStatus Status);
/// <summary>Payload for replacing a shift.</summary>
public sealed record UpdateShiftRequest(Guid BranchId, DateOnly Date, ShiftType Type, DateTimeOffset StartAt, DateTimeOffset EndAt, int RequiredStaff, bool IsOpen, ShiftStatus Status);

/// <summary>Shift-assignment data returned by the administrator API.</summary>
public sealed record ShiftAssignmentResponse(Guid Id, Guid ShiftId, Guid EmployeeId, ShiftAssignmentStatus Status, DateTimeOffset? RespondedAt);
/// <summary>Payload for creating a shift assignment.</summary>
public sealed record CreateShiftAssignmentRequest(Guid ShiftId, Guid EmployeeId, ShiftAssignmentStatus Status, DateTimeOffset? RespondedAt);
/// <summary>Payload for replacing a shift assignment.</summary>
public sealed record UpdateShiftAssignmentRequest(Guid ShiftId, Guid EmployeeId, ShiftAssignmentStatus Status, DateTimeOffset? RespondedAt);

/// <summary>Attendance data returned by the administrator API.</summary>
public sealed record AttendanceResponse(Guid Id, Guid EmployeeId, Guid ShiftId, DateTimeOffset? CheckInAt, DateTimeOffset? CheckOutAt, string Method, bool Approved);
/// <summary>Payload for creating attendance data.</summary>
public sealed record CreateAttendanceRequest(Guid EmployeeId, Guid ShiftId, DateTimeOffset? CheckInAt, DateTimeOffset? CheckOutAt, [property: Required, MaxLength(32)] string Method, bool Approved);
/// <summary>Payload for replacing attendance data.</summary>
public sealed record UpdateAttendanceRequest(Guid EmployeeId, Guid ShiftId, DateTimeOffset? CheckInAt, DateTimeOffset? CheckOutAt, [property: Required, MaxLength(32)] string Method, bool Approved);

/// <summary>Shift-closing data returned by the administrator API.</summary>
public sealed record ShiftClosingResponse(Guid Id, Guid ShiftId, Guid ClosedByEmployeeId, decimal ExpectedCash, decimal CountedCash, decimal Difference, string? Note, string Denominations);
/// <summary>Payload for creating shift-closing data.</summary>
public sealed record CreateShiftClosingRequest(Guid ShiftId, Guid ClosedByEmployeeId, decimal ExpectedCash, decimal CountedCash, decimal Difference, [property: MaxLength(1000)] string? Note, [property: Required] string Denominations);
/// <summary>Payload for replacing shift-closing data.</summary>
public sealed record UpdateShiftClosingRequest(Guid ShiftId, Guid ClosedByEmployeeId, decimal ExpectedCash, decimal CountedCash, decimal Difference, [property: MaxLength(1000)] string? Note, [property: Required] string Denominations);

/// <summary>Customer-session data returned by the administrator API.</summary>
public sealed record SessionResponse(Guid Id, Guid SeatId, Guid? CustomerId, Guid PricePlanId, DateTimeOffset StartAt, DateTimeOffset EndAt, SessionStatus Status);
/// <summary>Payload for creating a customer session.</summary>
public sealed record CreateSessionRequest(Guid SeatId, Guid? CustomerId, Guid PricePlanId, DateTimeOffset StartAt, DateTimeOffset EndAt, SessionStatus Status);
/// <summary>Payload for replacing a customer session.</summary>
public sealed record UpdateSessionRequest(Guid SeatId, Guid? CustomerId, Guid PricePlanId, DateTimeOffset StartAt, DateTimeOffset EndAt, SessionStatus Status);

/// <summary>Session-extension data returned by the administrator API.</summary>
public sealed record SessionExtensionResponse(Guid Id, Guid SessionId, int AddedMinutes, decimal Price, DateTimeOffset CreatedAt);
/// <summary>Payload for creating a session extension.</summary>
public sealed record CreateSessionExtensionRequest(Guid SessionId, int AddedMinutes, decimal Price, DateTimeOffset CreatedAt);
/// <summary>Payload for replacing a session extension.</summary>
public sealed record UpdateSessionExtensionRequest(Guid SessionId, int AddedMinutes, decimal Price, DateTimeOffset CreatedAt);

/// <summary>Bill data returned by the administrator API.</summary>
public sealed record BillResponse(Guid Id, string Code, Guid BranchId, Guid ShiftId, Guid SessionId, decimal Total, PaymentMethod PaymentMethod, Guid? ClaimedByCustomerId, DateTimeOffset? ClaimedAt);
/// <summary>Payload for creating a bill.</summary>
public sealed record CreateBillRequest([property: Required, MaxLength(64)] string Code, Guid BranchId, Guid ShiftId, Guid SessionId, decimal Total, PaymentMethod PaymentMethod, Guid? ClaimedByCustomerId, DateTimeOffset? ClaimedAt);
/// <summary>Payload for replacing a bill.</summary>
public sealed record UpdateBillRequest([property: Required, MaxLength(64)] string Code, Guid BranchId, Guid ShiftId, Guid SessionId, decimal Total, PaymentMethod PaymentMethod, Guid? ClaimedByCustomerId, DateTimeOffset? ClaimedAt);

/// <summary>Point-transaction data returned by the administrator API.</summary>
public sealed record PointTransactionResponse(Guid Id, Guid CustomerId, Guid? BillId, int Points, string Reason);
/// <summary>Payload for creating a point transaction.</summary>
public sealed record CreatePointTransactionRequest(Guid CustomerId, Guid? BillId, int Points, [property: Required, MaxLength(500)] string Reason);
/// <summary>Payload for replacing a point transaction.</summary>
public sealed record UpdatePointTransactionRequest(Guid CustomerId, Guid? BillId, int Points, [property: Required, MaxLength(500)] string Reason);

/// <summary>Expense data returned by the administrator API.</summary>
public sealed record ExpenseResponse(Guid Id, Guid BranchId, string Category, decimal Amount, DateTimeOffset Date, string? Note, Guid CreatedByEmployeeId);
/// <summary>Payload for creating an expense.</summary>
public sealed record CreateExpenseRequest(Guid BranchId, [property: Required, MaxLength(80)] string Category, decimal Amount, DateTimeOffset Date, [property: MaxLength(1000)] string? Note, Guid CreatedByEmployeeId);
/// <summary>Payload for replacing an expense.</summary>
public sealed record UpdateExpenseRequest(Guid BranchId, [property: Required, MaxLength(80)] string Category, decimal Amount, DateTimeOffset Date, [property: MaxLength(1000)] string? Note, Guid CreatedByEmployeeId);

/// <summary>Payroll data returned by the administrator API.</summary>
public sealed record PayrollResponse(Guid Id, Guid EmployeeId, DateOnly Month, decimal TotalHours, decimal BaseAmount, decimal Allowance, decimal Bonus, decimal Deduction, decimal NetAmount, string Status);
/// <summary>Payload for creating payroll data.</summary>
public sealed record CreatePayrollRequest(Guid EmployeeId, DateOnly Month, decimal TotalHours, decimal BaseAmount, decimal Allowance, decimal Bonus, decimal Deduction, decimal NetAmount, [property: Required, MaxLength(32)] string Status);
/// <summary>Payload for replacing payroll data.</summary>
public sealed record UpdatePayrollRequest(Guid EmployeeId, DateOnly Month, decimal TotalHours, decimal BaseAmount, decimal Allowance, decimal Bonus, decimal Deduction, decimal NetAmount, [property: Required, MaxLength(32)] string Status);

/// <summary>Employee-branch assignment data returned by the administrator API.</summary>
public sealed record EmployeeBranchResponse(Guid Id, Guid EmployeeId, Guid BranchId, bool IsHome);
/// <summary>Payload for creating an employee-branch assignment.</summary>
public sealed record CreateEmployeeBranchRequest(Guid EmployeeId, Guid BranchId, bool IsHome);
/// <summary>Payload for replacing an employee-branch assignment.</summary>
public sealed record UpdateEmployeeBranchRequest(Guid EmployeeId, Guid BranchId, bool IsHome);

/// <summary>Employee-availability data returned by the administrator API.</summary>
public sealed record AvailabilityResponse(Guid Id, Guid EmployeeId, bool IsRecurring, DayOfWeek? DayOfWeek, DateOnly? SpecificDate, TimeOnly From, TimeOnly To);
/// <summary>Payload for creating employee availability.</summary>
public sealed record CreateAvailabilityRequest(Guid EmployeeId, bool IsRecurring, DayOfWeek? DayOfWeek, DateOnly? SpecificDate, TimeOnly From, TimeOnly To);
/// <summary>Payload for replacing employee availability.</summary>
public sealed record UpdateAvailabilityRequest(Guid EmployeeId, bool IsRecurring, DayOfWeek? DayOfWeek, DateOnly? SpecificDate, TimeOnly From, TimeOnly To);

/// <summary>Shift-request data returned by the administrator API.</summary>
public sealed record ShiftRequestResponse(Guid Id, ShiftRequestType Type, Guid ShiftId, Guid RequesterEmployeeId, Guid? TargetEmployeeId, string Reason, string Status, Guid? ReviewedByEmployeeId, DateTimeOffset? ReviewedAt);
/// <summary>Payload for creating a shift request.</summary>
public sealed record CreateShiftRequestRequest(ShiftRequestType Type, Guid ShiftId, Guid RequesterEmployeeId, Guid? TargetEmployeeId, [property: Required, MaxLength(1000)] string Reason, [property: Required, MaxLength(32)] string Status, Guid? ReviewedByEmployeeId, DateTimeOffset? ReviewedAt);
/// <summary>Payload for replacing a shift request.</summary>
public sealed record UpdateShiftRequestRequest(ShiftRequestType Type, Guid ShiftId, Guid RequesterEmployeeId, Guid? TargetEmployeeId, [property: Required, MaxLength(1000)] string Reason, [property: Required, MaxLength(32)] string Status, Guid? ReviewedByEmployeeId, DateTimeOffset? ReviewedAt);

/// <summary>Notification data returned by the administrator API.</summary>
public sealed record NotificationResponse(Guid Id, Guid UserId, string Type, string Title, string Body, string? Link, DateTimeOffset? ReadAt);
/// <summary>Payload for creating a notification.</summary>
public sealed record CreateNotificationRequest(Guid UserId, [property: Required, MaxLength(64)] string Type, [property: Required, MaxLength(200)] string Title, [property: Required, MaxLength(2000)] string Body, [property: MaxLength(500)] string? Link, DateTimeOffset? ReadAt);
/// <summary>Payload for replacing a notification.</summary>
public sealed record UpdateNotificationRequest(Guid UserId, [property: Required, MaxLength(64)] string Type, [property: Required, MaxLength(200)] string Title, [property: Required, MaxLength(2000)] string Body, [property: MaxLength(500)] string? Link, DateTimeOffset? ReadAt);
