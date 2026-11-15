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
}
