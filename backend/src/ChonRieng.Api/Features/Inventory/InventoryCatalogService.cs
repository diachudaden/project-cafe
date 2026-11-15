using ChonRieng.Api.Features.Common;
using ChonRieng.Domain.Entities;
using ChonRieng.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChonRieng.Api.Features.Inventory;

/// <summary>Items, packing units, per-branch settings and balances.</summary>
public sealed class InventoryCatalogService(AppDbContext db, OuterStockCalculator calculator, TimeProvider clock)
{
    public async Task<IReadOnlyList<InventoryItemView>> ListItemsAsync(bool includeInactive, CancellationToken ct) =>
        (await db.InventoryItems.AsNoTracking().Include(i => i.Units)
            .Where(i => includeInactive || i.IsActive)
            .OrderBy(i => i.SortOrder).ThenBy(i => i.Name)
            .ToListAsync(ct))
        .Select(ToView).ToList();

    public async Task<Result<InventoryItemView>> CreateItemAsync(CurrentUser user, SaveInventoryItemRequest request, CancellationToken ct)
    {
        if (!user.IsAdmin && !user.IsManager)
        {
            return ServiceError.Forbidden("Chỉ quản lý được thêm mặt hàng.");
        }

        if (Validate(request) is { } invalid)
        {
            return invalid;
        }

        var name = request.Name.Trim();
        if (await db.InventoryItems.AnyAsync(i => i.Name == name, ct))
        {
            return ServiceError.Conflict("Trùng tên", $"Đã có mặt hàng \"{name}\".");
        }

        var item = new InventoryItem { CreatedAt = clock.GetUtcNow() };
        Apply(item, request);
        db.InventoryItems.Add(item);
        await db.SaveChangesAsync(ct);
        return ToView(item);
    }

    public async Task<Result<InventoryItemView>> UpdateItemAsync(CurrentUser user, Guid id, SaveInventoryItemRequest request, CancellationToken ct)
    {
        if (!user.IsAdmin && !user.IsManager)
        {
            return ServiceError.Forbidden("Chỉ quản lý được sửa mặt hàng.");
        }

        var item = await db.InventoryItems.Include(i => i.Units).SingleOrDefaultAsync(i => i.Id == id, ct);
        if (item is null)
        {
            return ServiceError.NotFound();
        }

        if (Validate(request) is { } invalid)
        {
            return invalid;
        }

        var name = request.Name.Trim();
        if (await db.InventoryItems.AnyAsync(i => i.Name == name && i.Id != id, ct))
        {
            return ServiceError.Conflict("Trùng tên", $"Đã có mặt hàng \"{name}\".");
        }

        // Lines keep the unit name they were entered with, so units can be replaced freely.
        db.ItemUnits.RemoveRange(item.Units);
        item.Units.Clear();
        Apply(item, request);
        await db.SaveChangesAsync(ct);
        return ToView(item);
    }

    public async Task<Result<IReadOnlyList<BranchSettingView>>> ListSettingsAsync(CurrentUser user, Guid branchId, CancellationToken ct)
    {
        if (!user.CanManageBranch(branchId))
        {
            return ServiceError.NotFound();
        }

        var items = await db.InventoryItems.AsNoTracking().OrderBy(i => i.SortOrder).ThenBy(i => i.Name).ToListAsync(ct);
        var settings = await db.ItemBranchSettings.AsNoTracking().Where(s => s.BranchId == branchId).ToDictionaryAsync(s => s.ItemId, ct);
        return Result<IReadOnlyList<BranchSettingView>>.From(items.Select(i =>
        {
            var s = settings.GetValueOrDefault(i.Id) ?? new ItemBranchSetting { ItemId = i.Id, BranchId = branchId };
            return new BranchSettingView(i.Id, i.Name, i.BaseUnit, i.IsActive, s.NormPerSession, s.NormOverride, s.EffectiveNorm,
                s.OuterMin, s.OuterPar, s.InnerMin, s.NormLearnedAt);
        }).ToList());
    }

    public async Task<Result<BranchSettingView>> SaveSettingAsync(CurrentUser user, Guid itemId, Guid branchId, SaveBranchSettingRequest request, CancellationToken ct)
    {
        var item = await db.InventoryItems.SingleOrDefaultAsync(i => i.Id == itemId, ct);
        if (item is null || !user.CanManageBranch(branchId) || !await db.Branches.AnyAsync(b => b.Id == branchId, ct))
        {
            return ServiceError.NotFound();
        }

        if (request.OuterPar < request.OuterMin)
        {
            return ServiceError.Invalid(nameof(request.OuterPar), "Mức tối đa kho ngoài phải ≥ mức tối thiểu.");
        }

        var setting = await db.ItemBranchSettings.SingleOrDefaultAsync(s => s.ItemId == itemId && s.BranchId == branchId, ct);
        if (setting is null)
        {
            setting = new ItemBranchSetting { ItemId = itemId, BranchId = branchId };
            db.ItemBranchSettings.Add(setting);
        }

        if (request.NormPerSession is { } norm)
        {
            setting.NormPerSession = Math.Round(norm, 3);
        }

        setting.NormOverride = request.NormOverride is { } o ? Math.Round(o, 3) : null;
        setting.OuterMin = request.OuterMin;
        setting.OuterPar = request.OuterPar;
        setting.InnerMin = request.InnerMin;
        await db.SaveChangesAsync(ct);
        return new BranchSettingView(item.Id, item.Name, item.BaseUnit, item.IsActive, setting.NormPerSession, setting.NormOverride,
            setting.EffectiveNorm, setting.OuterMin, setting.OuterPar, setting.InnerMin, setting.NormLearnedAt);
    }

    /// <summary>Book stock of one warehouse (Management only: the outer book would reveal the expected count).</summary>
    public async Task<Result<IReadOnlyList<BalanceView>>> BalancesAsync(CurrentUser user, Guid branchId, WarehouseType warehouse, CancellationToken ct)
    {
        if (!user.CanManageBranch(branchId) || !await db.Branches.AnyAsync(b => b.Id == branchId, ct))
        {
            return ServiceError.NotFound();
        }

        var (inner, outer) = await Warehouses.EnsureAsync(db, branchId, ct);
        var target = warehouse == WarehouseType.Inner ? inner : outer;
        var now = clock.GetUtcNow();
        IReadOnlyDictionary<Guid, OuterFigures>? figures = null;
        if (warehouse == WarehouseType.Outer)
        {
            var shift = await InventoryShifts.RunningAsync(db, branchId, now, ct);
            figures = await calculator.ComputeAsync(branchId, outer.Id, shift, now, ct);
        }

        var balances = await db.StockBalances.AsNoTracking().Where(b => b.WarehouseId == target.Id).ToDictionaryAsync(b => b.ItemId, b => b.Qty, ct);
        var rows = await db.InventoryItems.AsNoTracking().Where(i => i.IsActive).OrderBy(i => i.SortOrder).ThenBy(i => i.Name)
            .Select(i => new { i.Id, i.Name, i.BaseUnit, Setting = i.BranchSettings.FirstOrDefault(s => s.BranchId == branchId) })
            .ToListAsync(ct);
        return Result<IReadOnlyList<BalanceView>>.From(rows.Select(r =>
        {
            var qty = balances.GetValueOrDefault(r.Id);
            var min = warehouse == WarehouseType.Inner ? r.Setting?.InnerMin ?? 0 : r.Setting?.OuterMin ?? 0;
            int? expected = figures is not null && figures.TryGetValue(r.Id, out var f) ? f.Expected : null;
            return new BalanceView(r.Id, r.Name, r.BaseUnit, qty, min,
                warehouse == WarehouseType.Outer ? r.Setting?.OuterPar : null,
                (expected ?? qty) < min, expected);
        }).ToList());
    }

    private static ServiceError? Validate(SaveInventoryItemRequest request)
    {
        var units = request.Units ?? [];
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.BaseUnit))
        {
            return ServiceError.Invalid(nameof(request.Name), "Cần nhập tên và đơn vị gốc.");
        }

        if (units.Any(u => string.IsNullOrWhiteSpace(u.UnitName) || u.Factor < 2))
        {
            return ServiceError.Invalid(nameof(request.Units), "Đơn vị quy đổi cần tên và hệ số ≥ 2.");
        }

        var names = units.Select(u => u.UnitName.Trim().ToLowerInvariant()).Append(request.BaseUnit.Trim().ToLowerInvariant()).ToList();
        return names.Distinct().Count() != names.Count
            ? ServiceError.Invalid(nameof(request.Units), "Tên đơn vị bị trùng.")
            : null;
    }

    private static void Apply(InventoryItem item, SaveInventoryItemRequest request)
    {
        item.Name = request.Name.Trim();
        item.BaseUnit = request.BaseUnit.Trim();
        item.Category = request.Category.Trim();
        item.CountDaily = request.CountDaily;
        item.SortOrder = request.SortOrder;
        item.IsActive = request.IsActive;
        foreach (var unit in request.Units ?? [])
        {
            item.Units.Add(new ItemUnit { UnitName = unit.UnitName.Trim(), Factor = unit.Factor });
        }
    }

    public static InventoryItemView ToView(InventoryItem i) => new(i.Id, i.Name, i.BaseUnit, i.Category, i.IsActive, i.CountDaily, i.SortOrder,
        i.Units.OrderBy(u => u.Factor).Select(u => new ItemUnitView(u.UnitName, u.Factor)).ToList());
}
