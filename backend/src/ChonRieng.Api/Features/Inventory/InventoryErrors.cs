using ChonRieng.Api.Features.Common;

namespace ChonRieng.Api.Features.Inventory;

/// <summary>Business error codes, returned as <c>violations[0].rule</c> of a 409 ProblemDetails.</summary>
public static class InventoryErrors
{
    public const string ShiftNotOpen = "SHIFT_NOT_OPEN";
    public const string CountAlreadyConfirmed = "COUNT_ALREADY_CONFIRMED";
    public const string ReasonRequired = "REASON_REQUIRED";
    public const string InsufficientStock = "INSUFFICIENT_STOCK";
    public const string NotYourShift = "NOT_YOUR_SHIFT";
    public const string CountInProgress = "COUNT_IN_PROGRESS";
    public const string CountIncomplete = "COUNT_INCOMPLETE";
    public const string InvalidState = "INVALID_STATE";
    public const string Busy = "STOCK_CHANGED";

    public static ServiceError Conflict(string code, string title, string detail) =>
        ServiceError.Conflict(title, detail, [new RuleViolation(code, detail)]);

    public static ServiceError NoShift() =>
        Conflict(ShiftNotOpen, "Bạn chưa tham gia ca", "Hãy bấm 'Tham gia ca' tại chi nhánh này trước.");

    public static ServiceError Reason(string detail = "Số lượng vượt gợi ý hoặc vượt mức tối đa kho ngoài, vui lòng nhập lý do.") =>
        Conflict(ReasonRequired, "Cần nhập lý do", detail);

    public static ServiceError Retry() =>
        Conflict(Busy, "Tồn kho vừa thay đổi", "Có thao tác khác vừa ghi vào kho, vui lòng thử lại.");
}
