namespace ChonRieng.Application.Payments;

// INTEGRATE: dự án đã có kiểu Result riêng thì thay PayResult bằng Result của dự án
// và sửa PaymentEndpoints.ToHttp() cho khớp. Giữ nguyên các mã lỗi (Code) để frontend không phải đổi.

public enum PayErrorKind
{
    NotFound,
    Conflict,
    Validation,
    Forbidden,
}

public sealed record PayError(PayErrorKind Kind, string Code, string Message)
{
    public static PayError NotFound(string code, string message) => new(PayErrorKind.NotFound, code, message);
    public static PayError Conflict(string code, string message) => new(PayErrorKind.Conflict, code, message);
    public static PayError Validation(string code, string message) => new(PayErrorKind.Validation, code, message);
}

public static class PayErrors
{
    public static readonly PayError BillNotFound = PayError.NotFound("BILL_NOT_FOUND", "Không tìm thấy bill.");
    public static readonly PayError BillNotPayable = PayError.Conflict("BILL_NOT_PAYABLE", "Bill không ở trạng thái chờ thanh toán.");
    public static readonly PayError SeatNotFound = PayError.NotFound("SEAT_NOT_FOUND", "Không tìm thấy chỗ ngồi.");
    public static readonly PayError SeatTaken = PayError.Conflict("SEAT_TAKEN", "Chỗ này vừa có người giữ hoặc đang ngồi.");
    public static readonly PayError TransactionNotFound = PayError.NotFound("TRANSACTION_NOT_FOUND", "Không tìm thấy giao dịch.");
    public static readonly PayError TransactionResolved = PayError.Conflict("TRANSACTION_RESOLVED", "Giao dịch này đã được xử lý.");
    public static readonly PayError LedgerNotFound = PayError.NotFound("LEDGER_NOT_FOUND", "Không tìm thấy dòng thu chi.");
    public static readonly PayError LedgerAlreadyReversed = PayError.Conflict("LEDGER_ALREADY_REVERSED", "Dòng này đã được đảo trước đó.");
    public static readonly PayError LedgerIsReversal = PayError.Conflict("LEDGER_IS_REVERSAL", "Không thể đảo một bút toán đảo.");
    public static readonly PayError Busy = PayError.Conflict("BUSY", "Hệ thống đang bận, thử lại sau vài giây.");
}

public sealed class PayResult<T>
{
    private PayResult(T? value, PayError? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }
    public PayError? Error { get; }
    public bool IsSuccess => Error is null;

    public static PayResult<T> Ok(T value) => new(value, null);
    public static PayResult<T> Fail(PayError error) => new(default, error);

    public static implicit operator PayResult<T>(PayError error) => Fail(error);
}
