using System.ComponentModel.DataAnnotations;

namespace ChonRieng.Api.Features.Inventory;

/// <summary><c>Inventory__*</c> settings (TASK-027).</summary>
public sealed class InventoryOptions
{
    public const string SectionName = "Inventory";

    /// <summary>Tolerance floor in base units: max(this, round(usage × percent)).</summary>
    [Range(0, 1000)]
    public int ToleranceMinUnits { get; set; } = 2;

    [Range(0, 1)]
    public decimal TolerancePercent { get; set; } = 0.08m;

    /// <summary>Minimum length of the reason for an out-of-tolerance count.</summary>
    [Range(1, 200)]
    public int CountReasonMinLength { get; set; } = 10;

    /// <summary>Items with CountDaily = false are counted at shift close once every this many days.</summary>
    [Range(1, 31)]
    public int RotationDays { get; set; } = 3;

    /// <summary>Background jobs (reminders, restock, nightly reconciliation and norm learning). false = off.</summary>
    public bool JobsEnabled { get; set; } = true;
}
