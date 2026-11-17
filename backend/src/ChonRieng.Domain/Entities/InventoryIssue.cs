using ChonRieng.Domain.Common;

namespace ChonRieng.Domain.Entities;

/// <summary>Nightly two-warehouse reconciliation result for one item between two inner counts.</summary>
public sealed class InventoryIssue : Entity
{
    public Guid BranchId { get; set; }
    public Guid ItemId { get; set; }
    public DateTimeOffset PeriodFrom { get; set; }
    public DateTimeOffset PeriodTo { get; set; }
    public InventoryIssueKind Kind { get; set; }
    public int InnerVar { get; set; }
    public int OuterVar { get; set; }
    public int Total { get; set; }
    public int Tolerance { get; set; }
    public InventoryIssueStatus Status { get; set; } = InventoryIssueStatus.Open;
    /// <summary>The inner count that closes the period (one issue per item per inner count).</summary>
    public Guid InnerCountDocumentId { get; set; }
    /// <summary>TransferMisrecorded: the draft adjustment proposed.</summary>
    public Guid? AdjustmentDocumentId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? ResolvedBy { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public string? Note { get; set; }

    public Branch Branch { get; set; } = null!;
    public InventoryItem Item { get; set; } = null!;
}
