using System.Globalization;
using System.Text.RegularExpressions;

namespace ChonRieng.Domain.Payments;

/// <summary>
/// Mã thanh toán nằm trong nội dung chuyển khoản, dạng <c>{prefix}{billId}</c>, ví dụ <c>CR1024</c>.
/// </summary>
public static class PaymentCode
{
    private const int MaxDigits = 9;

    public static string For(string prefix, int billId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(billId);
        return string.Concat(prefix.ToUpperInvariant(), billId.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>Đọc mã SePay đã tách sẵn (trường <c>code</c>). Không phân biệt hoa thường.</summary>
    public static bool TryParse(string? code, string prefix, out int billId)
    {
        billId = 0;
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(prefix)) return false;

        var value = code.Trim();
        if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;

        var digits = value.AsSpan(prefix.Length);
        if (digits.IsEmpty || digits.Length > MaxDigits) return false;
        foreach (var c in digits)
        {
            if (!char.IsAsciiDigit(c)) return false;
        }

        return int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out billId) && billId > 0;
    }

    /// <summary>
    /// Dự phòng khi SePay không tách được <c>code</c>: tìm mã trong nội dung chuyển khoản gốc.
    /// Chỉ nhận khi có đúng một mã khớp, để tránh gán nhầm bill.
    /// </summary>
    public static bool TryFindInContent(string? content, string prefix, out int billId)
    {
        billId = 0;
        if (string.IsNullOrWhiteSpace(content) || string.IsNullOrWhiteSpace(prefix)) return false;

        var pattern = $@"(?<![A-Za-z0-9]){Regex.Escape(prefix)}(\d{{1,{MaxDigits}}})(?![0-9])";
        var matches = Regex.Matches(content, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));

        var distinct = matches.Select(m => m.Groups[1].Value).Distinct().ToList();
        if (distinct.Count != 1) return false;

        return int.TryParse(distinct[0], NumberStyles.None, CultureInfo.InvariantCulture, out billId) && billId > 0;
    }
}
