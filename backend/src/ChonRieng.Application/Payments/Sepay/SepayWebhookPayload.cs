namespace ChonRieng.Application.Payments.Sepay;

/// <summary>
/// Payload SePay gửi tới webhook (JSON, camelCase). Đọc bằng <c>JsonSerializerOptions.Web</c>.
/// </summary>
public sealed record SepayWebhookPayload(
    long Id,
    string Gateway,
    string TransactionDate,
    string AccountNumber,
    string? SubAccount,
    string? Code,
    string? Content,
    string TransferType,
    string? Description,
    long TransferAmount,
    long Accumulated,
    string? ReferenceCode)
{
    public bool IsIncoming => string.Equals(TransferType, "in", StringComparison.OrdinalIgnoreCase);
}
