using ChonRieng.Domain.Common;

namespace ChonRieng.Domain.Entities;

/// <summary>The next shift receiving an outer-warehouse count: accept, or dispute for a manager to resolve.</summary>
public sealed class ShiftCountHandover : Entity
{
    public Guid CountDocumentId { get; set; }
    public HandoverStatus Status { get; set; } = HandoverStatus.Waiting;
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? ReceivedBy { get; set; }
    public DateTimeOffset? ReceivedAt { get; set; }
    public Guid? ReceivedShiftId { get; set; }
    public string? DisputeNote { get; set; }
    /// <summary>JSON [{itemId, countedQty}]: the next shift's own count (the locked count document is never edited).</summary>
    public string? DisputeLines { get; set; }
    public Guid? ResolvedBy { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public string? ResolveNote { get; set; }
    /// <summary>The adjustment (DC) that moved the count to the manager's final quantities.</summary>
    public Guid? ResolveAdjustmentId { get; set; }
    /// <summary>Reminder sent to the POS (30 min) / manager (60 min).</summary>
    public DateTimeOffset? RemindedAt { get; set; }
    public DateTimeOffset? EscalatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public StockDocument CountDocument { get; set; } = null!;
}
