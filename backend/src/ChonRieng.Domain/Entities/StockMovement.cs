namespace ChonRieng.Domain.Entities;

/// <summary>Append-only stock ledger: never updated or deleted. Book stock = SUM(Qty).</summary>
public sealed class StockMovement
{
    public long Id { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid ItemId { get; set; }
    public int Qty { get; set; }
    public Guid DocumentId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }

    public Warehouse Warehouse { get; set; } = null!;
    public InventoryItem Item { get; set; } = null!;
    public StockDocument Document { get; set; } = null!;
}
