using ChonRieng.Domain.Common;

namespace ChonRieng.Domain.Entities;

/// <summary>
/// PN (receipt), PC (transfer), PH (waste), KK (count), DC (adjustment).
/// Confirmed/Approved documents are immutable; corrections are new Adjustment documents with <see cref="AdjustsDocumentId"/>.
/// </summary>
public sealed class StockDocument : Entity
{
    public string Code { get; set; } = "";
    public StockDocumentType Type { get; set; }
    public Guid BranchId { get; set; }
    public Guid? FromWarehouseId { get; set; }
    public Guid? ToWarehouseId { get; set; }
    /// <summary>Count documents: the shift being closed (outer) — null for weekly inner counts.</summary>
    public Guid? ShiftId { get; set; }
    public StockDocumentStatus Status { get; set; } = StockDocumentStatus.Draft;
    public string? Note { get; set; }
    public string? Reason { get; set; }
    public string? Supplier { get; set; }
    public string? InvoiceNo { get; set; }
    public string? PhotoUrl { get; set; }
    /// <summary>Count documents: sessions + extensions sold in the shift (snapshot at confirmation).</summary>
    public int? SessionCount { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? ConfirmedBy { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public Guid? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public string? ApprovalNote { get; set; }
    public Guid? AdjustsDocumentId { get; set; }
    /// <summary>SHA-256 (hex) of the confirmed lines + ConfirmedBy + ConfirmedAt.</summary>
    public string? PayloadHash { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public Branch Branch { get; set; } = null!;
    public Warehouse? FromWarehouse { get; set; }
    public Warehouse? ToWarehouse { get; set; }
    public Shift? Shift { get; set; }
    public StockDocument? AdjustsDocument { get; set; }
    public ICollection<StockDocumentLine> Lines { get; } = new List<StockDocumentLine>();

    public bool IsLocked => Status is StockDocumentStatus.Confirmed or StockDocumentStatus.Approved
        or StockDocumentStatus.PendingApproval or StockDocumentStatus.Rejected;
}
