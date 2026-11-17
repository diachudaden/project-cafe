using ChonRieng.Api.Features.Common;

namespace ChonRieng.Api.Features.Finance;

/// <summary>Lương = giờ công × đơn giá + giờ đêm (22:00–06:00) × phụ cấp + thưởng − phạt (reference workflow step 6).</summary>
public static class PayrollCalculator
{
    private static readonly TimeOnly NightStart = new(22, 0);
    private static readonly TimeOnly NightEnd = new(6, 0);

    public static (decimal Hours, decimal NightHours) Hours(DateTimeOffset checkIn, DateTimeOffset checkOut)
    {
        if (checkOut <= checkIn)
        {
            return (0, 0);
        }

        var start = checkIn.ToVietnam();
        var end = checkOut.ToVietnam();
        var night = TimeSpan.Zero;
        // Night windows that may overlap: the one starting the evening before check-in through the day of check-out.
        for (var day = DateOnly.FromDateTime(start.DateTime).AddDays(-1); day <= DateOnly.FromDateTime(end.DateTime); day = day.AddDays(1))
        {
            var windowStart = new DateTimeOffset(day.ToDateTime(NightStart), VietnamTime.Offset);
            var windowEnd = new DateTimeOffset(day.AddDays(1).ToDateTime(NightEnd), VietnamTime.Offset);
            var overlapStart = start > windowStart ? start : windowStart;
            var overlapEnd = end < windowEnd ? end : windowEnd;
            if (overlapEnd > overlapStart)
            {
                night += overlapEnd - overlapStart;
            }
        }

        return (Round2((decimal)(end - start).TotalHours), Round2((decimal)night.TotalHours));
    }

    public static decimal Amount(decimal hours, decimal nightHours, decimal hourlyRate, decimal nightAllowance) =>
        Vnd(hours * hourlyRate) + Vnd(nightHours * nightAllowance);

    public static decimal Vnd(decimal value) => Math.Round(value, 0, MidpointRounding.AwayFromZero);

    private static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
