using ChonRieng.Domain.Common;

namespace ChonRieng.Domain.Entities;

public sealed class PaymentTransaction : Entity
{
    public string Provider { get; set; } = "SePay";
    public string ProviderTxId { get; set; } = "";
    public string AccountNumber { get; set; } = "";
    public long Amount { get; set; }
    public string? Code { get; set; }
    public string? Content { get; set; }
    public string? ReferenceCode { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public PaymentMatchStatus MatchStatus { get; set; } = PaymentMatchStatus.Unmatched;
    public Guid? PaymentRequestId { get; set; }
    public Guid? ResolvedByUserId { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public string? Note { get; set; }

    public PaymentRequest? PaymentRequest { get; set; }
}
