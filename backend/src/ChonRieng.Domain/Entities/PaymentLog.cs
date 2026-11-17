using ChonRieng.Domain.Common;

namespace ChonRieng.Domain.Entities;

/// <summary>
/// One step in the life of a VietQR payment (request created, polled, webhook received/rejected,
/// matched, settled, expired...). Append-only trace used to find where a payment got stuck.
/// </summary>
public sealed class PaymentLog : Entity
{
    public DateTimeOffset OccurredAt { get; set; }
    public PaymentLogStage Stage { get; set; }
    public PaymentLogLevel Level { get; set; }
    public Guid? PaymentRequestId { get; set; }
    public string? PaymentCode { get; set; }
    public Guid? PaymentTransactionId { get; set; }
    public string? ProviderTxId { get; set; }
    public Guid? BillId { get; set; }
    public string? BillCode { get; set; }
    public Guid? BranchId { get; set; }
    public decimal? Amount { get; set; }
    public string Message { get; set; } = "";
    /// <summary>Small JSON object with stage-specific facts; never secrets, raw bodies or transfer content.</summary>
    public string? Detail { get; set; }
    public string? TraceId { get; set; }
    /// <summary>Milliseconds since the payment request was created, when known.</summary>
    public long? ElapsedMs { get; set; }
}

public enum PaymentLogStage : byte
{
    RequestCreated = 1,
    StatusPolled = 2,
    WebhookReceived = 3,
    WebhookRejected = 4,
    WebhookDuplicate = 5,
    TransactionRecorded = 6,
    TransactionIgnored = 7,
    TransactionUnmatched = 8,
    CodeMatched = 9,
    AmountMismatch = 10,
    LatePayment = 11,
    AlreadyPaid = 12,
    SettlementFailed = 13,
    Settled = 14,
    Expired = 15,
    Cancelled = 16,
    Exception = 17,
}

public enum PaymentLogLevel : byte
{
    Info = 1,
    Warning = 2,
    Error = 3,
}
