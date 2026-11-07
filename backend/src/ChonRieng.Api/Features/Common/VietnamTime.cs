namespace ChonRieng.Api.Features.Common;

/// <summary>Business-day helpers. Vietnam has no daylight saving, so a fixed UTC+7 offset is exact.</summary>
public static class VietnamTime
{
    public static readonly TimeSpan Offset = TimeSpan.FromHours(7);

    public static DateTimeOffset Now(this TimeProvider clock) => clock.GetUtcNow().ToOffset(Offset);

    public static DateOnly Today(this TimeProvider clock) => DateOnly.FromDateTime(clock.Now().DateTime);

    public static DateTimeOffset ToVietnam(this DateTimeOffset value) => value.ToOffset(Offset);

    public static DateTimeOffset StartOfDay(DateOnly date) => new(date.ToDateTime(TimeOnly.MinValue), Offset);

    /// <summary>Monday of the week containing <paramref name="date"/>.</summary>
    public static DateOnly WeekStart(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    public static DateOnly MonthStart(DateOnly date) => new(date.Year, date.Month, 1);

    /// <summary>Whether the branch is open at local time <paramref name="localTime"/>, handling closing after midnight.</summary>
    public static bool IsOpen(bool is24h, TimeOnly open, TimeOnly close, TimeOnly localTime)
    {
        if (is24h)
        {
            return true;
        }

        return open <= close
            ? localTime >= open && localTime < close
            : localTime >= open || localTime < close;
    }
}
