using ChonRieng.Api.Features.Catalog;
using ChonRieng.Api.Features.Common;
using ChonRieng.Api.Features.Finance;
using ChonRieng.Api.Features.Sales;
using ChonRieng.Api.Features.Scheduling;
using ChonRieng.Domain.Entities;
using Xunit;

namespace ChonRieng.UnitTests.Features;

public sealed class RulesAndCalculatorsTests
{
    private static readonly Guid BranchA = Guid.NewGuid();
    private static readonly Guid BranchB = Guid.NewGuid();

    private static DateTimeOffset At(int day, int hour) => new(2026, 11, day, hour, 0, 0, VietnamTime.Offset);

    private static ShiftRuleContext Context(ShiftSlot candidate, params ShiftSlot[] others) =>
        new(Guid.NewGuid(), candidate, others, new HashSet<Guid> { BranchA, BranchB }, false);

    [Fact]
    public void Overlap_is_blocked_even_across_branches()
    {
        var violations = ShiftConflictChecker.Default.Check(Context(
            new ShiftSlot(Guid.NewGuid(), BranchA, At(2, 8), At(2, 15)),
            new ShiftSlot(Guid.NewGuid(), BranchB, At(2, 14), At(2, 22))));

        Assert.Contains(violations, v => v.Rule == "overlap" && v.IsBlocking && !v.CanOverride);
        Assert.False(ShiftConflictChecker.IsAllowed(violations, "cần người"));
    }

    [Fact]
    public void Short_rest_blocks_but_manager_can_override_with_reason()
    {
        var violations = ShiftConflictChecker.Default.Check(Context(
            new ShiftSlot(Guid.NewGuid(), BranchA, At(3, 6), At(3, 14)),
            new ShiftSlot(Guid.NewGuid(), BranchA, At(2, 22), At(3, 0))));

        var rest = Assert.Single(violations, v => v.Rule == "rest");
        Assert.True(rest.CanOverride);
        Assert.False(ShiftConflictChecker.IsAllowed(violations, null));
        Assert.True(ShiftConflictChecker.IsAllowed(violations, "Thiếu người ca sáng"));
    }

    [Fact]
    public void Travel_between_branches_cannot_be_overridden()
    {
        var violations = ShiftConflictChecker.Default.Check(Context(
            new ShiftSlot(Guid.NewGuid(), BranchB, At(2, 15).AddMinutes(30), At(2, 22)),
            new ShiftSlot(Guid.NewGuid(), BranchA, At(2, 8), At(2, 15))));

        Assert.Contains(violations, v => v.Rule == "travel" && !v.CanOverride);
        Assert.False(ShiftConflictChecker.IsAllowed(violations, "lý do"));
    }

    [Fact]
    public void Unassigned_branch_and_approved_leave_block()
    {
        var candidate = new ShiftSlot(Guid.NewGuid(), Guid.NewGuid(), At(5, 8), At(5, 15));
        var violations = ShiftConflictChecker.Default.Check(new ShiftRuleContext(Guid.NewGuid(), candidate, [], new HashSet<Guid> { BranchA }, true));

        Assert.Contains(violations, v => v.Rule == "branch");
        Assert.Contains(violations, v => v.Rule == "leave");
    }

    [Fact]
    public void More_than_48_hours_a_week_is_only_a_warning()
    {
        // Mon 2 Nov 2026 … : six 8-hour shifts with 16 h rest, plus a 7th → 56 h.
        var others = Enumerable.Range(0, 6).Select(i => new ShiftSlot(Guid.NewGuid(), BranchA, At(2 + i, 6), At(2 + i, 14))).ToArray();
        var violations = ShiftConflictChecker.Default.Check(Context(new ShiftSlot(Guid.NewGuid(), BranchA, At(8, 6), At(8, 14)), others));

        var weekly = Assert.Single(violations);
        Assert.Equal("weekly-hours", weekly.Rule);
        Assert.False(weekly.IsBlocking);
        Assert.True(ShiftConflictChecker.IsAllowed(violations, null));
    }

    [Theory]
    [InlineData(22, 6, 8, 8)]
    [InlineData(14, 22, 8, 0)]
    [InlineData(20, 23, 3, 1)]
    [InlineData(5, 9, 4, 1)]
    public void Night_hours_are_counted_between_22_and_6(int startHour, int endHour, decimal hours, decimal night)
    {
        var start = At(2, startHour);
        var end = endHour > startHour ? At(2, endHour) : At(3, endHour);

        var (total, nightHours) = PayrollCalculator.Hours(start, end);

        Assert.Equal(hours, total);
        Assert.Equal(night, nightHours);
        Assert.Equal(PayrollCalculator.Vnd(hours * 25_000 + night * 10_000), PayrollCalculator.Amount(total, nightHours, 25_000, 10_000));
    }

    [Fact]
    public void Points_are_one_per_thousand_dong_and_tiers_follow_total_spent()
    {
        Assert.Equal(45, Loyalty.PointsFor(45_000));
        Assert.Equal(0, Loyalty.PointsFor(999));
        Assert.Equal(Loyalty.Bronze, Loyalty.TierFor(499_999));
        Assert.Equal(Loyalty.Silver, Loyalty.TierFor(500_000));
        Assert.Equal(Loyalty.Gold, Loyalty.TierFor(2_000_000));
        Assert.Matches("^[A-HJ-NP-Z2-9]{8}$", Loyalty.NewBillCode());
    }

    [Fact]
    public void All_valid_plans_are_offered_branch_first_and_expired_or_foreign_plans_are_ignored()
    {
        var now = At(10, 12);
        var chain = new PricePlan { Name = "chain", DurationMinutes = 240, Price = 45_000 };
        var branchA = new PricePlan { BranchId = BranchA, Name = "A", DurationMinutes = 240, Price = 40_000 };
        var studentA = new PricePlan { BranchId = BranchA, Name = "A student", DurationMinutes = 240, Price = 30_000 };
        var expired = new PricePlan { BranchId = BranchB, Name = "old", DurationMinutes = 240, Price = 1_000, ValidTo = now.AddDays(-1) };

        Assert.Equal([studentA, branchA, chain], CatalogService.ResolveActivePlans([chain, branchA, expired, studentA], BranchA, now));
        Assert.Same(chain, Assert.Single(CatalogService.ResolveActivePlans([chain, branchA, expired, studentA], BranchB, now)));
    }

    [Fact]
    public void Branch_opening_hours_handle_closing_after_midnight()
    {
        Assert.True(VietnamTime.IsOpen(false, new TimeOnly(8, 0), new TimeOnly(23, 0), new TimeOnly(22, 59)));
        Assert.False(VietnamTime.IsOpen(false, new TimeOnly(8, 0), new TimeOnly(23, 0), new TimeOnly(23, 0)));
        Assert.True(VietnamTime.IsOpen(false, new TimeOnly(18, 0), new TimeOnly(2, 0), new TimeOnly(1, 0)));
        Assert.True(VietnamTime.IsOpen(true, default, default, new TimeOnly(3, 0)));
    }
}
