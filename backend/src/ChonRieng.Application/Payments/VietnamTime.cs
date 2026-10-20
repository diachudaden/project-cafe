using System.Globalization;

namespace ChonRieng.Application.Payments;

/// <summary>Việt Nam cố định UTC+7, không có giờ mùa hè, nên cộng trừ thẳng là đủ.</summary>
public static class VietnamTime
{
    public static readonly TimeSpan Offset = TimeSpan.FromHours(7);

    public static DateTime ToUtc(DateTime vietnamLocal) =>
        DateTime.SpecifyKind(vietnamLocal - Offset, DateTimeKind.Utc);

    public static DateTime FromUtc(DateTime utc) =>
        DateTime.SpecifyKind(utc + Offset, DateTimeKind.Unspecified);

    public static DateOnly Today(DateTime nowUtc) => DateOnly.FromDateTime(FromUtc(nowUtc));

    /// <summary>[00:00, 24:00) giờ VN của ngày <paramref name="date"/>, đổi ra UTC.</summary>
    public static (DateTime FromUtc, DateTime ToUtc) DayRangeUtc(DateOnly date)
    {
        var start = ToUtc(date.ToDateTime(TimeOnly.MinValue));
        return (start, start.AddDays(1));
    }

    /// <summary>Đọc "yyyy-MM-dd HH:mm:ss" giờ VN (định dạng của SePay) ra UTC.</summary>
    public static bool TryParseSepay(string? value, out DateTime utc)
    {
        utc = default;
        if (!DateTime.TryParseExact(value, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var local))
            return false;

        utc = ToUtc(local);
        return true;
    }
}
