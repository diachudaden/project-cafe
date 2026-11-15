using ChonRieng.Domain.Common;

namespace ChonRieng.Domain.Entities;

public sealed class StockDocumentLine : Entity
{
    public Guid DocumentId { get; set; }
    public Guid ItemId { get; set; }
    /// <summary>Base units (signed for adjustments).</summary>
    public int Qty { get; set; }
    /// <summary>What the user typed, e.g. 2 × "Thùng".</summary>
    public string? UnitName { get; set; }
    public int? UnitQty { get; set; }
    public decimal? UnitCost { get; set; }
    public DateOnly? ExpiryDate { get; set; }

    // Count documents only.
    /// <summary>First blind count; written once and never changed.</summary>
    public int? FirstCountedQty { get; set; }
    public int? CountedQty { get; set; }
    public int? OpeningQty { get; set; }
    public int? TransferInQty { get; set; }
    public int? WasteQty { get; set; }
    public decimal? Norm { get; set; }
    public decimal? Usage { get; set; }
    public int? ExpectedQty { get; set; }
    public int? Variance { get; set; }
    public int? Tolerance { get; set; }

    // Transfer documents only.
    public int? SuggestedQty { get; set; }

    public StockDocument Document { get; set; } = null!;
    public InventoryItem Item { get; set; } = null!;
}
