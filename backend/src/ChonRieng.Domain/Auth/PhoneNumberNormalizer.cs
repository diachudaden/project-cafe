using System.Text;

namespace ChonRieng.Domain.Auth;

public static class PhoneNumberNormalizer
{
    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var trimmed = input.Trim();
        var builder = new StringBuilder(trimmed.Length);
        foreach (var ch in trimmed)
        {
            if (ch is '+' or (>= '0' and <= '9'))
            {
                builder.Append(ch);
            }
        }

        var digits = builder.ToString();
        if (digits.Length == 0)
        {
            return null;
        }

        if (digits.StartsWith("+84", StringComparison.Ordinal))
        {
            digits = "0" + digits[3..];
        }
        else if (digits.StartsWith("84", StringComparison.Ordinal) && digits.Length >= 11)
        {
            digits = "0" + digits[2..];
        }

        return digits.All(char.IsDigit) ? digits : null;
    }
}
