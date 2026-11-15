namespace ChonRieng.Domain.Entities;

/// <summary>Cached book stock per (warehouse, item); updated in the same transaction as its movements.</summary>
public sealed class StockBalance
{
    public Guid WarehouseId { get; set; }
    public Guid ItemId { get; set; }
    public int Qty { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public Warehouse Warehouse { get; set; } = null!;
    public InventoryItem Item { get; set; } = null!;
}
