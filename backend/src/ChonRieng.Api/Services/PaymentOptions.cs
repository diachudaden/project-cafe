namespace ChonRieng.Api.Services;

public sealed class PaymentOptions
{
    public const string SectionName = "Payment";

    public string Bank { get; set; } = "SePay";
    public string AccountNumber { get; set; } = "0000000000";
    public string AccountHolder { get; set; } = "Cafe CR";
    public string CodePrefix { get; set; } = "TT";
    public string QrImageBaseUrl { get; set; } = "https://qr.sepay.vn/img";
    public int HoldMinutes { get; set; } = 10;
    public string WebhookSecret { get; set; } = "demo-secret";
    public int TimestampToleranceSeconds { get; set; } = 300;
    public bool ValidateOnStart { get; set; } = true;

    public void ValidateOrThrow()
    {
        if (string.IsNullOrWhiteSpace(Bank))
        {
            throw new InvalidOperationException("Payment__Bank is required.");
        }

        if (string.IsNullOrWhiteSpace(AccountNumber))
        {
            throw new InvalidOperationException("Payment__AccountNumber is required.");
        }

        if (string.IsNullOrWhiteSpace(AccountHolder))
        {
            throw new InvalidOperationException("Payment__AccountHolder is required.");
        }

        if (string.IsNullOrWhiteSpace(CodePrefix))
        {
            throw new InvalidOperationException("Payment__CodePrefix is required.");
        }

        if (string.IsNullOrWhiteSpace(QrImageBaseUrl))
        {
            throw new InvalidOperationException("Payment__QrImageBaseUrl is required.");
        }

        if (HoldMinutes <= 0)
        {
            throw new InvalidOperationException("Payment__HoldMinutes must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(WebhookSecret))
        {
            throw new InvalidOperationException("Payment__WebhookSecret is required.");
        }

        if (TimestampToleranceSeconds <= 0)
        {
            throw new InvalidOperationException("Payment__TimestampToleranceSeconds must be greater than zero.");
        }
    }
}
