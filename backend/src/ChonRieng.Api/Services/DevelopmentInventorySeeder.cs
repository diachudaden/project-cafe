using ChonRieng.Api.Features.Inventory;
using ChonRieng.Domain.Entities;
using ChonRieng.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChonRieng.Api.Services;

/// <summary>Demo inventory: 2 warehouses per branch, 5 items, branch settings and an opening receipt + transfer.</summary>
internal static class DevelopmentInventorySeeder
{
    private sealed record ItemSpec(
        string Name, string BaseUnit, string Category, bool CountDaily, int Order,
        (string Unit, int Factor)[] Units, decimal Norm, int OuterMin, int OuterPar, int InnerMin, int InnerOpening, int OuterOpening, decimal UnitCost);

    private static readonly ItemSpec[] Items =
    [
        new("[DEMO] Cà phê 3in1", "gói", "Đồ uống", true, 1, [("Hộp", 20), ("Thùng", 480)], 0.6m, 20, 60, 100, 480, 40, 3_000m),
        new("[DEMO] Trà túi lọc", "túi", "Đồ uống", true, 2, [("Hộp", 25)], 0.3m, 10, 30, 50, 200, 20, 1_500m),
        new("[DEMO] Sữa đặc", "lon", "Nguyên liệu", true, 3, [("Thùng", 48)], 0.1m, 2, 6, 12, 48, 4, 25_000m),
        new("[DEMO] Ly giấy", "cái", "Vật tư", true, 4, [("Cây", 50), ("Thùng", 1000)], 1.0m, 30, 100, 300, 1000, 80, 500m),
        new("[DEMO] Đường gói", "gói", "Nguyên liệu", false, 5, [("Túi", 100)], 0.5m, 20, 80, 200, 500, 60, 200m),
    ];

    public static async Task SeedAsync(AppDbContext db, IReadOnlyList<Branch> branches, Guid createdBy, DateTimeOffset now, CancellationToken ct)
    {
        var items = new List<(InventoryItem Item, ItemSpec Spec)>();
        foreach (var spec in Items)
        {
            var item = await db.InventoryItems.Include(i => i.Units).SingleOrDefaultAsync(i => i.Name == spec.Name, ct);
            if (item is null)
            {
                item = new InventoryItem
                {
                    Name = spec.Name, BaseUnit = spec.BaseUnit, Category = spec.Category, CountDaily = spec.CountDaily,
                    SortOrder = spec.Order, IsActive = true, CreatedAt = now,
                };
                foreach (var (unit, factor) in spec.Units)
                {
                    item.Units.Add(new ItemUnit { UnitName = unit, Factor = factor });
                }

                db.InventoryItems.Add(item);
            }

            items.Add((item, spec));
        }

        await db.SaveChangesAsync(ct);

        foreach (var branch in branches)
        {
            var (inner, outer) = await Warehouses.EnsureAsync(db, branch.Id, ct);
            foreach (var (item, spec) in items)
            {
                if (!await db.ItemBranchSettings.AnyAsync(s => s.ItemId == item.Id && s.BranchId == branch.Id, ct))
                {
                    db.ItemBranchSettings.Add(new ItemBranchSetting
                    {
                        ItemId = item.Id, BranchId = branch.Id, NormPerSession = spec.Norm,
                        OuterMin = spec.OuterMin, OuterPar = spec.OuterPar, InnerMin = spec.InnerMin,
                    });
                }
            }

            // Opening stock only once per branch: one receipt into the inner warehouse, one transfer to the outer one.
            if (!await db.StockMovements.AnyAsync(m => m.WarehouseId == inner.Id || m.WarehouseId == outer.Id, ct))
            {
                var receipt = NewDocument(StockDocumentType.Receipt, branch.Id, null, inner.Id, createdBy, now,
                    await DocumentCodes.NextAsync(db, StockDocumentType.Receipt, branch.Id, now, ct));
                receipt.Supplier = "[DEMO] Nhà cung cấp mẫu";
                receipt.Note = "Tồn đầu kỳ (dữ liệu mẫu)";
                var transfer = NewDocument(StockDocumentType.Transfer, branch.Id, inner.Id, outer.Id, createdBy, now,
                    await DocumentCodes.NextAsync(db, StockDocumentType.Transfer, branch.Id, now, ct));
                transfer.Note = "Lôi hàng đầu kỳ (dữ liệu mẫu)";
                db.StockDocuments.AddRange(receipt, transfer);
                foreach (var (item, spec) in items)
                {
                    receipt.Lines.Add(new StockDocumentLine { ItemId = item.Id, Qty = spec.InnerOpening, UnitCost = spec.UnitCost });
                    transfer.Lines.Add(new StockDocumentLine { ItemId = item.Id, Qty = spec.OuterOpening });
                    await StockLedger.PostAsync(db, inner.Id, item.Id, spec.InnerOpening, receipt.Id, now, ct);
                    await StockLedger.PostAsync(db, inner.Id, item.Id, -spec.OuterOpening, transfer.Id, now, ct);
                    await StockLedger.PostAsync(db, outer.Id, item.Id, spec.OuterOpening, transfer.Id, now, ct);
                }
            }

            await db.SaveChangesAsync(ct);
        }
    }

    private static StockDocument NewDocument(
        StockDocumentType type, Guid branchId, Guid? from, Guid? to, Guid createdBy, DateTimeOffset now, string code) => new()
    {
        Code = code, Type = type, BranchId = branchId, FromWarehouseId = from, ToWarehouseId = to,
        Status = StockDocumentStatus.Confirmed, CreatedBy = createdBy, CreatedAt = now, ConfirmedBy = createdBy, ConfirmedAt = now,
    };
}
