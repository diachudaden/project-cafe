using ChonRieng.Domain.Entities;
using ChonRieng.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChonRieng.Api.Features.Inventory;

/// <summary>
/// The only writer of <see cref="StockMovement"/> and <see cref="StockBalance"/>: appends a movement and moves the cached
/// balance in the caller's unit of work. Concurrent writers conflict on the balance RowVersion (or its primary key).
/// </summary>
public static class StockLedger
{
    public static async Task<StockBalance> BalanceAsync(AppDbContext db, Guid warehouseId, Guid itemId, CancellationToken ct)
    {
        var balance = db.StockBalances.Local.FirstOrDefault(b => b.WarehouseId == warehouseId && b.ItemId == itemId)
            ?? await db.StockBalances.FirstOrDefaultAsync(b => b.WarehouseId == warehouseId && b.ItemId == itemId, ct);
        if (balance is null)
        {
            balance = new StockBalance { WarehouseId = warehouseId, ItemId = itemId, Qty = 0 };
            db.StockBalances.Add(balance);
        }

        return balance;
    }

    public static async Task<StockBalance> PostAsync(
        AppDbContext db, Guid warehouseId, Guid itemId, int qty, Guid documentId, DateTimeOffset at, CancellationToken ct)
    {
        var balance = await BalanceAsync(db, warehouseId, itemId, ct);
        if (qty == 0)
        {
            return balance;
        }

        balance.Qty += qty;
        db.StockMovements.Add(new StockMovement
        {
            WarehouseId = warehouseId, ItemId = itemId, Qty = qty, DocumentId = documentId, OccurredAt = at,
        });
        return balance;
    }

    /// <summary>
    /// Posts an adjustment (DC): each signed line moves <c>+Qty</c> into ToWarehouse and <c>−Qty</c> out of FromWarehouse.
    /// Returns the item that would drive the inner (exact) warehouse negative, or null when posted.
    /// </summary>
    public static async Task<(Guid ItemId, int Available, int Needed)?> PostAdjustmentAsync(
        AppDbContext db, StockDocument doc, IReadOnlyDictionary<Guid, WarehouseType> warehouseTypes, DateTimeOffset at, CancellationToken ct)
    {
        foreach (var line in doc.Lines)
        {
            foreach (var (warehouseId, qty) in new[] { (doc.ToWarehouseId, line.Qty), (doc.FromWarehouseId, -line.Qty) })
            {
                if (warehouseId is not { } id || qty == 0)
                {
                    continue;
                }

                var balance = await BalanceAsync(db, id, line.ItemId, ct);
                if (qty < 0 && warehouseTypes.GetValueOrDefault(id) == WarehouseType.Inner && balance.Qty + qty < 0)
                {
                    return (line.ItemId, balance.Qty, -qty);
                }

                await PostAsync(db, id, line.ItemId, qty, doc.Id, at, ct);
            }
        }

        return null;
    }
}
