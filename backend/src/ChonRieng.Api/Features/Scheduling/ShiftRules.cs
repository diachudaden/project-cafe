using ChonRieng.Api.Features.Common;

namespace ChonRieng.Api.Features.Scheduling;

/// <summary>A shift an employee holds or is offered, reduced to what the rules need.</summary>
public sealed record ShiftSlot(Guid ShiftId, Guid BranchId, DateTimeOffset StartAt, DateTimeOffset EndAt)
{
    public double Hours => (EndAt - StartAt).TotalHours;
}

public sealed record ShiftRuleContext(
    Guid EmployeeId,
    ShiftSlot Candidate,
    IReadOnlyCollection<ShiftSlot> OtherShifts,
    IReadOnlySet<Guid> AssignedBranchIds,
    bool HasApprovedLeave);

/// <summary>One scheduling rule; new rules are added as new classes (reference: IShiftRule / ShiftConflictChecker).</summary>
public interface IShiftRule
{
    IEnumerable<RuleViolation> Check(ShiftRuleContext context);
}

public sealed class OverlapRule : IShiftRule
{
    public IEnumerable<RuleViolation> Check(ShiftRuleContext c) =>
        c.OtherShifts
            .Where(o => o.StartAt < c.Candidate.EndAt && c.Candidate.StartAt < o.EndAt)
            .Select(o => new RuleViolation("overlap",
                $"Trùng giờ với ca {o.StartAt.ToVietnam():dd/MM HH:mm}–{o.EndAt.ToVietnam():HH:mm}" + (o.BranchId != c.Candidate.BranchId ? " ở chi nhánh khác." : ".")));
}

public sealed class BranchAssignmentRule : IShiftRule
{
    public IEnumerable<RuleViolation> Check(ShiftRuleContext c)
    {
        if (!c.AssignedBranchIds.Contains(c.Candidate.BranchId))
        {
            yield return new RuleViolation("branch", "Nhân viên chưa được gán vào chi nhánh này.");
        }
    }
}

public sealed class ApprovedLeaveRule : IShiftRule
{
    public IEnumerable<RuleViolation> Check(ShiftRuleContext c)
    {
        if (c.HasApprovedLeave)
        {
            yield return new RuleViolation("leave", "Nhân viên đã được duyệt nghỉ ca này.");
        }
    }
}

public sealed class RestRule(TimeSpan minimumRest) : IShiftRule
{
    public IEnumerable<RuleViolation> Check(ShiftRuleContext c)
    {
        foreach (var o in c.OtherShifts)
        {
            var gap = o.EndAt <= c.Candidate.StartAt ? c.Candidate.StartAt - o.EndAt
                : c.Candidate.EndAt <= o.StartAt ? o.StartAt - c.Candidate.EndAt
                : (TimeSpan?)null; // overlap is reported by OverlapRule
            if (gap is { } g && g < minimumRest)
            {
                yield return new RuleViolation("rest",
                    $"Chỉ nghỉ {g.TotalHours:0.#} giờ giữa hai ca (tối thiểu {minimumRest.TotalHours:0} giờ).",
                    CanOverride: true);
            }
        }
    }
}

public sealed class TravelRule(TimeSpan minimumTravel) : IShiftRule
{
    public IEnumerable<RuleViolation> Check(ShiftRuleContext c)
    {
        foreach (var o in c.OtherShifts.Where(o => o.BranchId != c.Candidate.BranchId))
        {
            var gap = o.EndAt <= c.Candidate.StartAt ? c.Candidate.StartAt - o.EndAt
                : c.Candidate.EndAt <= o.StartAt ? o.StartAt - c.Candidate.EndAt
                : (TimeSpan?)null;
            if (gap is { } g && g < minimumTravel)
            {
                yield return new RuleViolation("travel",
                    $"Không đủ {minimumTravel.TotalMinutes:0} phút di chuyển giữa hai chi nhánh.");
            }
        }
    }
}

public sealed class WeeklyHoursRule(double maximumHours) : IShiftRule
{
    public IEnumerable<RuleViolation> Check(ShiftRuleContext c)
    {
        var weekStart = VietnamTime.StartOfDay(VietnamTime.WeekStart(DateOnly.FromDateTime(c.Candidate.StartAt.ToVietnam().DateTime)));
        var weekEnd = weekStart.AddDays(7);
        var total = c.Candidate.Hours + c.OtherShifts.Where(o => o.StartAt >= weekStart && o.StartAt < weekEnd).Sum(o => o.Hours);
        if (total > maximumHours)
        {
            yield return new RuleViolation("weekly-hours",
                $"Tổng {total:0.#} giờ trong tuần, vượt {maximumHours:0} giờ.", IsBlocking: false);
        }
    }
}

public sealed class ShiftConflictChecker(IEnumerable<IShiftRule> rules)
{
    /// <summary>Default rules from the project reference.</summary>
    public static ShiftConflictChecker Default { get; } = new(
    [
        new OverlapRule(),
        new BranchAssignmentRule(),
        new ApprovedLeaveRule(),
        new RestRule(TimeSpan.FromHours(8)),
        new TravelRule(TimeSpan.FromMinutes(60)),
        new WeeklyHoursRule(48),
    ]);

    public IReadOnlyList<RuleViolation> Check(ShiftRuleContext context) =>
        rules.SelectMany(rule => rule.Check(context)).ToList();

    /// <summary>Warnings never block; blocking violations pass only if all are overridable and a reason is given.</summary>
    public static bool IsAllowed(IReadOnlyList<RuleViolation> violations, string? overrideReason)
    {
        var blocking = violations.Where(v => v.IsBlocking).ToList();
        return blocking.Count == 0
            || (!string.IsNullOrWhiteSpace(overrideReason) && blocking.All(v => v.CanOverride));
    }
}
