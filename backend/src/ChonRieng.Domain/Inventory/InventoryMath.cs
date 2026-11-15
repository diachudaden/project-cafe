using ChonRieng.Domain.Entities;

namespace ChonRieng.Domain.Inventory;

/// <summary>Result of one outer-warehouse item for a shift.</summary>
public sealed record ShiftLineResult(
    int Opening, int TransfersIn, int Waste, int Sessions, decimal Norm, decimal Usage,
    int Expected, int? Counted, int? Variance, int Tolerance)
{
    /// <summary>Null until counted.</summary>
    public bool? WithinTolerance => Variance is null ? null : Math.Abs(Variance.Value) <= Tolerance;
}

public sealed record ReconcileResult(InventoryIssueKind Kind, int InnerVar, int OuterVar, int Total, int Tolerance);

/// <summary>
/// Pure inventory formulas (TASK-027). Quantities are base units; rounding is half away from zero.
/// </summary>
public static class InventoryMath
{
    public const int DefaultToleranceMinUnits = 2;
    public const decimal DefaultTolerancePercent = 0.08m;
    public const decimal NormClampPercent = 0.30m;
    public const decimal ParFactor = 1.2m;

    public static int Round(decimal value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);

    /// <summary>Usage = Sessions × Norm (each session or extension includes one drink).</summary>
    public static decimal Usage(int sessions, decimal norm) => sessions * norm;

    /// <summary>Expected = round(Opening + TransfersIn − Waste − Usage).</summary>
    public static int Expected(int opening, int transfersIn, int waste, decimal usage) =>
        Round(opening + transfersIn - waste - usage);

    /// <summary>Tolerance = max(minUnits, round(Usage × percent)).</summary>
    public static int Tolerance(decimal usage, int minUnits = DefaultToleranceMinUnits, decimal percent = DefaultTolerancePercent) =>
        Math.Max(minUnits, Round(usage * percent));

    public static ShiftLineResult EvaluateShiftLine(
        int opening, int transfersIn, int waste, int sessions, decimal norm, int? counted,
        int minUnits = DefaultToleranceMinUnits, decimal percent = DefaultTolerancePercent)
    {
        var usage = Usage(sessions, norm);
        var expected = Expected(opening, transfersIn, waste, usage);
        return new ShiftLineResult(
            opening, transfersIn, waste, sessions, norm, usage, expected,
            counted, counted - expected, Tolerance(usage, minUnits, percent));
    }

    /// <summary>Restock suggestion: max(0, OuterPar − ExpectedNow).</summary>
    public static int SuggestedTransfer(int outerPar, int expectedNow) => Math.Max(0, outerPar - expectedNow);

    /// <summary>A transfer above the suggestion, or one that overfills the par level, needs a reason (never blocked).</summary>
    public static bool TransferNeedsReason(int qty, int suggested, int expectedNow, int outerPar) =>
        qty > suggested || expectedNow + qty > outerPar;

    /// <summary>Converts "2 hộp + 5 gói" to base units: Σ qty × factor (factor 1 = base unit).</summary>
    public static int ToBaseUnits(IEnumerable<(int Qty, int Factor)> parts) => parts.Sum(p => checked(p.Qty * p.Factor));

    /// <summary>
    /// Classifies one item between two inner counts.
    /// InnerVar = counted − book (inner, at the count); OuterVar = Σ outer shift variances in the period.
    /// </summary>
    public static ReconcileResult Reconcile(int innerVar, int outerVar, decimal totalUsage,
        int minUnits = DefaultToleranceMinUnits, decimal percent = DefaultTolerancePercent)
    {
        var total = innerVar + outerVar;
        var tolerance = Tolerance(totalUsage, minUnits, percent);
        var kind = innerVar == 0 && Math.Abs(outerVar) <= tolerance
            ? InventoryIssueKind.Normal
            : innerVar != 0 && Math.Sign(innerVar) != Math.Sign(outerVar) && Math.Abs(total) <= tolerance
                ? InventoryIssueKind.TransferMisrecorded
                : total < -tolerance
                    ? InventoryIssueKind.RealLoss
                    : total > tolerance
                        ? InventoryIssueKind.UnexplainedSurplus
                        : InventoryIssueKind.WithinTolerance;
        return new ReconcileResult(kind, innerVar, outerVar, total, tolerance);
    }

    /// <summary>Actual usage of a confirmed shift count: Opening + In − Waste − Counted.</summary>
    public static int ActualUsage(int opening, int transfersIn, int waste, int counted) => opening + transfersIn - waste - counted;

    /// <summary>
    /// New norm = median of (actual usage / sessions) samples, clamped to ±30 % of the current norm
    /// (no clamp when the current norm is 0). No samples → current norm. Rounded to 3 decimals.
    /// </summary>
    public static decimal RecalcNorm(IEnumerable<decimal> usagePerSession, decimal currentNorm, decimal clampPercent = NormClampPercent)
    {
        var samples = usagePerSession.Order().ToArray();
        if (samples.Length == 0)
        {
            return currentNorm;
        }

        var mid = samples.Length / 2;
        var median = samples.Length % 2 == 1 ? samples[mid] : (samples[mid - 1] + samples[mid]) / 2m;
        if (median < 0)
        {
            median = 0;
        }

        if (currentNorm > 0)
        {
            median = Math.Clamp(median, currentNorm * (1 - clampPercent), currentNorm * (1 + clampPercent));
        }

        return Math.Round(median, 3, MidpointRounding.AwayFromZero);
    }

    /// <summary>Par = ceil(average usage per shift × 1.2); no samples → null (keep the current par).</summary>
    public static int? RecalcPar(IEnumerable<decimal> usagePerShift, decimal factor = ParFactor)
    {
        var samples = usagePerShift.ToArray();
        return samples.Length == 0 ? null : (int)Math.Ceiling(Math.Max(0, samples.Average()) * factor);
    }
}
