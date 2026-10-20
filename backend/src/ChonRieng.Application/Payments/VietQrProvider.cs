using ChonRieng.Domain.Payments;
using Microsoft.Extensions.Options;

namespace ChonRieng.Application.Payments;

/// <summary>
/// Tạo URL ảnh VietQR trỏ về tài khoản nhận tiền. Không gọi API nào — chỉ ghép URL.
/// </summary>
public sealed class VietQrProvider(IOptions<PaymentOptions> options) : IPaymentProvider
{
    public PaymentMethod Method => PaymentMethod.VietQr;

    public PaymentRequest CreateRequest(IPayableBill bill, DateTime expiresAtUtc)
    {
        var o = options.Value;
        var code = PaymentCode.For(o.CodePrefix, bill.Id);
        var description = string.IsNullOrWhiteSpace(o.DescriptionPrefix) ? code : $"{o.DescriptionPrefix.Trim()} {code}";

        var query = new List<string>
        {
            $"acc={Uri.EscapeDataString(o.AccountNumber)}",
            $"bank={Uri.EscapeDataString(o.Bank)}",
            $"amount={bill.AmountDue}",
            $"des={Uri.EscapeDataString(description)}",
        };
        if (!string.IsNullOrWhiteSpace(o.QrTemplate))
            query.Add($"template={Uri.EscapeDataString(o.QrTemplate)}");

        var url = $"{o.QrImageBaseUrl.TrimEnd('/')}?{string.Join('&', query)}";
        return new PaymentRequest(code, bill.AmountDue, url, expiresAtUtc);
    }
}
