using System.ComponentModel.DataAnnotations;

namespace ChonRieng.Api.Features.Sales;

/// <summary>Configuration section <c>Sessions</c> (env: <c>Sessions__AutoReleaseGraceMinutes</c>, ...).</summary>
public sealed class SessionReleaseOptions
{
    public const string SectionName = "Sessions";

    /// <summary>Minutes past <c>EndAt</c> before an unextended session is closed automatically; 0 disables it.</summary>
    [Range(0, 240)]
    public int AutoReleaseGraceMinutes { get; set; } = 10;

    /// <summary>How often the auto-release worker runs.</summary>
    [Range(10, 3600)]
    public int AutoReleaseIntervalSeconds { get; set; } = 60;

    public bool AutoReleaseEnabled => AutoReleaseGraceMinutes > 0;

    /// <summary>When a session ending at <paramref name="endAt"/> will be released, or null when disabled.</summary>
    public DateTimeOffset? AutoReleaseAt(DateTimeOffset endAt) =>
        AutoReleaseEnabled ? endAt.AddMinutes(AutoReleaseGraceMinutes) : null;
}
