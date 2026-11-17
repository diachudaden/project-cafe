using ChonRieng.Domain.Entities;
using ChonRieng.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChonRieng.Api.Features.Inventory;

/// <summary>Every branch has one inner and one outer warehouse; they are created on first use.</summary>
public static class Warehouses
{
    public const string InnerName = "Kho trong";
    public const string OuterName = "Kho ngoài";

    public static async Task<(Warehouse Inner, Warehouse Outer)> EnsureAsync(AppDbContext db, Guid branchId, CancellationToken ct)
    {
        var existing = await db.Warehouses.Where(w => w.BranchId == branchId).ToListAsync(ct);
        var inner = existing.FirstOrDefault(w => w.Type == WarehouseType.Inner);
        var outer = existing.FirstOrDefault(w => w.Type == WarehouseType.Outer);
        if (inner is null)
        {
            inner = new Warehouse { BranchId = branchId, Type = WarehouseType.Inner, Name = InnerName };
            db.Warehouses.Add(inner);
        }

        if (outer is null)
        {
            outer = new Warehouse { BranchId = branchId, Type = WarehouseType.Outer, Name = OuterName };
            db.Warehouses.Add(outer);
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(ct);
        }

        return (inner, outer);
    }
}
