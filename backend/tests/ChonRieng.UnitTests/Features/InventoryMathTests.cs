using ChonRieng.Domain.Entities;
using ChonRieng.Domain.Inventory;
using Xunit;

namespace ChonRieng.UnitTests.Features;

public sealed class InventoryMathTests
{
    [Fact]
    public void Sample_shift_line_is_out_of_tolerance()
    {
        // Opening 42, transfer in 72, 64 sessions × 1.2 = 76.8 → Expected round(37.2) = 37.
        var line = InventoryMath.EvaluateShiftLine(opening: 42, transfersIn: 72, waste: 0, sessions: 64, norm: 1.2m, counted: 22);

        Assert.Equal(76.8m, line.Usage);
        Assert.Equal(37, line.Expected);
        Assert.Equal(-15, line.Variance);
        Assert.Equal(6, line.Tolerance); // max(2, round(6.144))
        Assert.False(line.WithinTolerance);
    }

    [Fact]
    public void Exact_count_is_within_tolerance_and_uncounted_has_no_verdict()
    {
        Assert.True(InventoryMath.EvaluateShiftLine(42, 72, 0, 64, 1.2m, counted: 31).WithinTolerance); // −6
        Assert.Null(InventoryMath.EvaluateShiftLine(42, 72, 0, 64, 1.2m, counted: null).WithinTolerance);
    }

    [Theory]
    [InlineData(0, 2)]       // no sales: floor of 2 units
    [InlineData(10, 2)]      // 0.8 → 1, floor wins
    [InlineData(31.25, 3)]   // 2.5 → 3 (half away from zero)
    [InlineData(100, 8)]
    public void Tolerance_is_max_of_floor_and_percent(decimal usage, int expected) =>
        Assert.Equal(expected, InventoryMath.Tolerance(usage));

    [Fact]
    public void Waste_reduces_expected()
    {
        Assert.Equal(35, InventoryMath.Expected(42, 72, 2, 76.8m));
        Assert.Equal(3, InventoryMath.Expected(0, 0, 0, -2.5m)); // midpoint rounds away from zero
    }

    [Theory]
    [InlineData(60, 22, 38)]
    [InlineData(60, 75, 0)]
    public void Suggested_transfer_fills_to_par(int par, int expectedNow, int suggested) =>
        Assert.Equal(suggested, InventoryMath.SuggestedTransfer(par, expectedNow));

    [Fact]
    public void Transfer_needs_reason_above_suggestion_or_par()
    {
        Assert.False(InventoryMath.TransferNeedsReason(qty: 38, suggested: 38, expectedNow: 22, outerPar: 60));
        Assert.True(InventoryMath.TransferNeedsReason(qty: 40, suggested: 38, expectedNow: 22, outerPar: 60));
        Assert.True(InventoryMath.TransferNeedsReason(qty: 1, suggested: 0, expectedNow: 60, outerPar: 60));
    }

    [Fact]
    public void Base_units_from_packing_units() =>
        Assert.Equal(45, InventoryMath.ToBaseUnits([(2, 20), (5, 1)])); // 2 hộp × 20 + 5 gói

    // Period usage 100 → tolerance max(2, 8) = 8.
    [Theory]
    [InlineData(0, -3, InventoryIssueKind.Normal)]                 // (a) normal shrink within tolerance
    [InlineData(-10, 10, InventoryIssueKind.TransferMisrecorded)]  // (b) transferred 70, recorded 60
    [InlineData(-10, 0, InventoryIssueKind.RealLoss)]              // (c) 10 lost from the storeroom
    [InlineData(0, -13, InventoryIssueKind.RealLoss)]              // (d) outer short 13
    [InlineData(0, 12, InventoryIssueKind.UnexplainedSurplus)]     // (e) 12 surplus
    [InlineData(-3, -3, InventoryIssueKind.WithinTolerance)]       // small loss on both sides
    public void Reconcile_classifies_the_five_situations(int innerVar, int outerVar, InventoryIssueKind kind)
    {
        var result = InventoryMath.Reconcile(innerVar, outerVar, totalUsage: 100m);

        Assert.Equal(kind, result.Kind);
        Assert.Equal(innerVar + outerVar, result.Total);
        Assert.Equal(8, result.Tolerance);
    }

    [Fact]
    public void Recalc_norm_takes_median_and_clamps_to_30_percent()
    {
        Assert.Equal(1.1m, InventoryMath.RecalcNorm([1.0m, 1.1m, 5.0m], currentNorm: 1.0m));       // median, outlier ignored
        Assert.Equal(1.05m, InventoryMath.RecalcNorm([1.0m, 1.1m], currentNorm: 1.0m));            // even count: mean of middle two
        Assert.Equal(1.3m, InventoryMath.RecalcNorm([2.0m, 2.2m, 2.4m], currentNorm: 1.0m));       // clamp up
        Assert.Equal(0.7m, InventoryMath.RecalcNorm([0.1m, 0.2m, 0.3m], currentNorm: 1.0m));       // clamp down
        Assert.Equal(1.0m, InventoryMath.RecalcNorm([], currentNorm: 1.0m));                         // no samples
        Assert.Equal(0.42m, InventoryMath.RecalcNorm([0.42m], currentNorm: 0m));                     // first learn: no clamp
    }

    [Fact]
    public void Recalc_par_is_average_usage_times_1_2()
    {
        Assert.Equal(60, InventoryMath.RecalcPar([40m, 50m, 60m]));
        Assert.Equal(13, InventoryMath.RecalcPar([10.5m]));
        Assert.Null(InventoryMath.RecalcPar([]));
    }

    [Fact]
    public void Actual_usage_comes_from_the_count() =>
        Assert.Equal(92, InventoryMath.ActualUsage(42, 72, 0, 22));
}
