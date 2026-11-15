using ChonRieng.Api.Features.Common;
using ChonRieng.Domain.Entities;
using ChonRieng.Domain.Inventory;
using ChonRieng.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChonRieng.Api.Features.Inventory;

/// <summary>Receipts (PN), transfers inner → outer (PC) and waste (PH). Each is confirmed and locked on creation.</summary>
public sealed class StockDocumentService(AppDbContext db, OuterStockCalculator calculator, TimeProvider clock)
{
    public const string IngredientsExpenseCategory = "Nguyên liệu";
    private const int MaxLines = 50;
    private const int MaxAttempts = 3;

    public async Task<Result<StockDocumentView>> CreateReceiptAsync(CurrentUser user, CreateReceiptRequest request, CancellationToken ct)
    {
        if (!user.CanManageBranch(request.BranchId) || !await db.Branches.AnyAsync(b => b.Id == request.BranchId, ct))
        {
            return ServiceError.NotFound();
        }

        if (request.Lines is not { Count: > 0 and <= MaxLines } || request.Lines.Select(l => l.ItemId).Distinct().Count() != request.Lines.Count)
        {
            return ServiceError.Invalid(nameof(request.Lines), $"Cần 1–{MaxLines} dòng, mỗi mặt hàng một dòng.");
        }

        var items = await LoadItemsAsync(request.Lines.Select(l => l.ItemId), ct);
        if (items is null)
        {
            return ServiceError.Invalid(nameof(request.Lines), "Có mặt hàng không tồn tại hoặc đã ngừng dùng.");
        }

        var lines = new List<(ReceiptLineInput Input, int Qty)>();
        foreach (var line in request.Lines)
        {
            var item = items[line.ItemId];
            var factor = 1;
            if (!string.IsNullOrWhiteSpace(line.UnitName) && !string.Equals(line.UnitName.Trim(), item.BaseUnit, StringComparison.OrdinalIgnoreCase))
            {
                var unit = item.Units.FirstOrDefault(u => string.Equals(u.UnitName, line.UnitName.Trim(), StringComparison.OrdinalIgnoreCase));
                if (unit is null)
                {
                    return ServiceError.Invalid(nameof(request.Lines), $"{item.Name} không có đơn vị \"{line.UnitName}\".");
                }

                factor = unit.Factor;
            }

            lines.Add((line, InventoryMath.ToBaseUnits([(line.UnitQty, factor)])));
        }

        var total = lines.Sum(l => l.Input.UnitQty * l.Input.UnitCost);
        if (request.Paid && total <= 0)
        {
            return ServiceError.Invalid(nameof(request.Lines), "Phiếu nhập đã thanh toán cần có đơn giá.");
        }

        var (inner, _) = await Warehouses.EnsureAsync(db, request.BranchId, ct);
        return await WithRetryAsync(async () =>
        {
            var now = clock.GetUtcNow();
            var doc = NewDocument(StockDocumentType.Receipt, request.BranchId, null, inner.Id, null, user, now,
                await DocumentCodes.NextAsync(db, StockDocumentType.Receipt, request.BranchId, now, ct));
            doc.Supplier = request.Supplier?.Trim();
            doc.InvoiceNo = request.InvoiceNo?.Trim();
            doc.PhotoUrl = request.PhotoUrl?.Trim();
            doc.Note = request.Note?.Trim();
            foreach (var (input, qty) in lines)
            {
                doc.Lines.Add(new StockDocumentLine
                {
                    ItemId = input.ItemId, Qty = qty, UnitName = input.UnitName?.Trim(), UnitQty = input.UnitQty,
                    UnitCost = Math.Round(input.UnitCost, 0), ExpiryDate = input.ExpiryDate,
                });
                await StockLedger.PostAsync(db, inner.Id, input.ItemId, qty, doc.Id, now, ct);
            }

            if (request.Paid)
            {
                db.Expenses.Add(new Expense
                {
                    BranchId = request.BranchId,
                    Category = IngredientsExpenseCategory,
                    Amount = Math.Round(total, 0),
                    Date = now,
                    Note = Truncate($"{doc.Code}{Part(doc.Supplier)}{Part(doc.InvoiceNo is null ? null : "HĐ " + doc.InvoiceNo)}{Part(MethodLabel(request.Method))}", 1000),
                    StockDocumentId = doc.Id,
                    CreatedByUserId = user.UserId,
                    CreatedByEmployeeId = await db.Employees.Where(e => e.UserId == user.UserId).Select(e => (Guid?)e.Id).SingleOrDefaultAsync(ct),
                });
            }

            db.StockDocuments.Add(doc);
            await db.SaveChangesAsync(ct);
            return doc.Id;
        }, ct);
    }

    public async Task<Result<IReadOnlyList<TransferSuggestion>>> SuggestAsync(CurrentUser user, Guid branchId, CancellationToken ct)
    {
        if (!user.CanAccessBranch(branchId) || !await db.Branches.AnyAsync(b => b.Id == branchId, ct))
        {
            return ServiceError.NotFound();
        }

        var now = clock.GetUtcNow();
        var shift = await InventoryShifts.CurrentAsync(db, user, branchId, now, ct);
        if (!shift.IsSuccess)
        {
            return shift.Error!;
        }

        if (shift.Value is { } s && await InventoryShifts.CountInProgressAsync(db, s.Id, ct))
        {
            return CountInProgress();
        }

        var (inner, outer) = await Warehouses.EnsureAsync(db, branchId, ct);
        var figures = await calculator.ComputeAsync(branchId, outer.Id, shift.Value, now, ct);
        var innerQty = await db.StockBalances.AsNoTracking().Where(b => b.WarehouseId == inner.Id).ToDictionaryAsync(b => b.ItemId, b => b.Qty, ct);
        var rows = await db.ItemBranchSettings.AsNoTracking()
            .Where(x => x.BranchId == branchId && x.Item.IsActive)
            .OrderBy(x => x.Item.SortOrder).ThenBy(x => x.Item.Name)
            .Select(x => new { x.Item, x.Item.Units, x.OuterPar, x.OuterMin })
            .ToListAsync(ct);
        return Result<IReadOnlyList<TransferSuggestion>>.From(rows.Select(r =>
        {
            var expected = figures.TryGetValue(r.Item.Id, out var f) ? f.Expected : 0;
            return new TransferSuggestion(r.Item.Id, r.Item.Name, r.Item.BaseUnit,
                r.Units.OrderBy(u => u.Factor).Select(u => new ItemUnitView(u.UnitName, u.Factor)).ToList(),
                expected, r.OuterPar, r.OuterMin, Math.Min(InventoryMath.SuggestedTransfer(r.OuterPar, expected), Math.Max(0, innerQty.GetValueOrDefault(r.Item.Id))),
                innerQty.GetValueOrDefault(r.Item.Id));
        }).ToList());
    }

    public async Task<Result<StockDocumentView>> CreateTransferAsync(CurrentUser user, CreateTransferRequest request, CancellationToken ct)
    {
        if (!user.CanAccessBranch(request.BranchId) || !await db.Branches.AnyAsync(b => b.Id == request.BranchId, ct))
        {
            return ServiceError.NotFound();
        }

        if (ValidateLines(request.Lines) is { } invalid)
        {
            return invalid;
        }

        if (await LoadItemsAsync(request.Lines.Select(l => l.ItemId), ct) is null)
        {
            return ServiceError.Invalid(nameof(request.Lines), "Có mặt hàng không tồn tại hoặc đã ngừng dùng.");
        }

        var now = clock.GetUtcNow();
        var shift = await InventoryShifts.CurrentAsync(db, user, request.BranchId, now, ct);
        if (!shift.IsSuccess)
        {
            return shift.Error!;
        }

        if (shift.Value is { } s && await InventoryShifts.CountInProgressAsync(db, s.Id, ct))
        {
            return CountInProgress();
        }

        var (inner, outer) = await Warehouses.EnsureAsync(db, request.BranchId, ct);
        var figures = await calculator.ComputeAsync(request.BranchId, outer.Id, shift.Value, now, ct);
        var pars = await db.ItemBranchSettings.AsNoTracking().Where(x => x.BranchId == request.BranchId)
            .ToDictionaryAsync(x => x.ItemId, x => x.OuterPar, ct);
        var suggestions = request.Lines.ToDictionary(l => l.ItemId, l =>
        {
            var expected = figures.TryGetValue(l.ItemId, out var f) ? f.Expected : 0;
            var par = pars.GetValueOrDefault(l.ItemId);
            return (Expected: expected, Par: par, Suggested: InventoryMath.SuggestedTransfer(par, expected));
        });
        var needsReason = request.Lines.Any(l =>
        {
            var x = suggestions[l.ItemId];
            return InventoryMath.TransferNeedsReason(l.Qty, x.Suggested, x.Expected, x.Par);
        });
        if (needsReason && string.IsNullOrWhiteSpace(request.Reason))
        {
            return InventoryErrors.Reason();
        }

        return await WithRetryAsync(async () =>
        {
            var at = clock.GetUtcNow();
            var doc = NewDocument(StockDocumentType.Transfer, request.BranchId, inner.Id, outer.Id, shift.Value?.Id, user, at,
                await DocumentCodes.NextAsync(db, StockDocumentType.Transfer, request.BranchId, at, ct));
            doc.Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
            foreach (var line in request.Lines)
            {
                var balance = await StockLedger.BalanceAsync(db, inner.Id, line.ItemId, ct);
                if (balance.Qty < line.Qty)
                {
                    var name = await db.InventoryItems.Where(i => i.Id == line.ItemId).Select(i => i.Name).SingleAsync(ct);
                    throw new StockRuleException(InventoryErrors.Conflict(InventoryErrors.InsufficientStock, "Kho trong không đủ hàng",
                        $"{name}: kho trong còn {balance.Qty}, cần {line.Qty}."));
                }

                doc.Lines.Add(new StockDocumentLine { ItemId = line.ItemId, Qty = line.Qty, SuggestedQty = suggestions[line.ItemId].Suggested });
                await StockLedger.PostAsync(db, inner.Id, line.ItemId, -line.Qty, doc.Id, at, ct);
                await StockLedger.PostAsync(db, outer.Id, line.ItemId, line.Qty, doc.Id, at, ct);
            }

            db.StockDocuments.Add(doc);
            await db.SaveChangesAsync(ct);
            return doc.Id;
        }, ct);
    }

    public async Task<Result<StockDocumentView>> CreateWasteAsync(CurrentUser user, CreateWasteRequest request, CancellationToken ct)
    {
        if (!user.CanAccessBranch(request.BranchId) || !await db.Branches.AnyAsync(b => b.Id == request.BranchId, ct))
        {
            return ServiceError.NotFound();
        }

        if (ValidateLines(request.Lines) is { } invalid)
        {
            return invalid;
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return InventoryErrors.Reason("Phiếu huỷ cần ghi lý do.");
        }

        if (await LoadItemsAsync(request.Lines.Select(l => l.ItemId), ct) is null)
        {
            return ServiceError.Invalid(nameof(request.Lines), "Có mặt hàng không tồn tại hoặc đã ngừng dùng.");
        }

        var now = clock.GetUtcNow();
        var shift = await InventoryShifts.CurrentAsync(db, user, request.BranchId, now, ct);
        if (!shift.IsSuccess)
        {
            return shift.Error!;
        }

        if (request.Warehouse == WarehouseType.Outer && shift.Value is { } s && await InventoryShifts.CountInProgressAsync(db, s.Id, ct))
        {
            return CountInProgress();
        }

        var (inner, outer) = await Warehouses.EnsureAsync(db, request.BranchId, ct);
        var warehouse = request.Warehouse == WarehouseType.Inner ? inner : outer;
        return await WithRetryAsync(async () =>
        {
            var at = clock.GetUtcNow();
            var doc = NewDocument(StockDocumentType.Waste, request.BranchId, warehouse.Id, null, shift.Value?.Id, user, at,
                await DocumentCodes.NextAsync(db, StockDocumentType.Waste, request.BranchId, at, ct));
            doc.Reason = request.Reason.Trim();
            doc.PhotoUrl = request.PhotoUrl?.Trim();
            foreach (var line in request.Lines)
            {
                // The inner book is exact, so it cannot go negative; the outer book is only an estimate until the next count.
                var balance = await StockLedger.BalanceAsync(db, warehouse.Id, line.ItemId, ct);
                if (warehouse.Type == WarehouseType.Inner && balance.Qty < line.Qty)
                {
                    throw new StockRuleException(InventoryErrors.Conflict(InventoryErrors.InsufficientStock, "Kho trong không đủ hàng",
                        $"Kho trong còn {balance.Qty}, không thể huỷ {line.Qty}."));
                }

                doc.Lines.Add(new StockDocumentLine { ItemId = line.ItemId, Qty = line.Qty });
                await StockLedger.PostAsync(db, warehouse.Id, line.ItemId, -line.Qty, doc.Id, at, ct);
            }

            db.StockDocuments.Add(doc);
            await db.SaveChangesAsync(ct);
            return doc.Id;
        }, ct);
    }

    public async Task<Result<IReadOnlyList<StockDocumentView>>> ListAsync(
        CurrentUser user, Guid branchId, StockDocumentType? type, DateOnly from, DateOnly to, CancellationToken ct)
    {
        if (!user.CanManageBranch(branchId))
        {
            return ServiceError.NotFound();
        }

        var start = VietnamTime.StartOfDay(from);
        var end = VietnamTime.StartOfDay(to.AddDays(1));
        var ids = await db.StockDocuments.AsNoTracking()
            .Where(d => d.BranchId == branchId && d.CreatedAt >= start && d.CreatedAt < end && (type == null || d.Type == type))
            .OrderByDescending(d => d.CreatedAt).Take(300).Select(d => d.Id).ToListAsync(ct);
        return Result<IReadOnlyList<StockDocumentView>>.From(await ViewsAsync(ids, ct));
    }

    public async Task<Result<StockDocumentView>> GetAsync(CurrentUser user, Guid id, CancellationToken ct)
    {
        var doc = await db.StockDocuments.AsNoTracking().Where(d => d.Id == id).Select(d => new { d.BranchId, d.Type }).SingleOrDefaultAsync(ct);
        // Count documents have their own blind-count endpoints.
        if (doc is null || doc.Type == StockDocumentType.Count || !user.CanManageBranch(doc.BranchId))
        {
            return ServiceError.NotFound();
        }

        return (await ViewsAsync([id], ct)).Single();
    }

    public async Task<IReadOnlyList<StockDocumentView>> ViewsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        var docs = await db.StockDocuments.AsNoTracking()
            .Where(d => ids.Contains(d.Id))
            .Include(d => d.Lines).ThenInclude(l => l.Item)
            .Include(d => d.FromWarehouse).Include(d => d.ToWarehouse)
            .AsSplitQuery()
            .ToListAsync(ct);
        var userIds = docs.Select(d => d.CreatedBy).Distinct().ToList();
        var names = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        var expenses = await db.Expenses.AsNoTracking().Where(e => e.StockDocumentId != null && ids.Contains(e.StockDocumentId!.Value))
            .ToDictionaryAsync(e => e.StockDocumentId!.Value, e => e.Id, ct);
        var byId = docs.ToDictionary(d => d.Id);
        return ids.Where(byId.ContainsKey).Select(id =>
        {
            var d = byId[id];
            return new StockDocumentView(d.Id, d.Code, d.Type, d.Status, d.BranchId, d.FromWarehouse?.Type, d.ToWarehouse?.Type, d.ShiftId,
                d.Note, d.Reason, d.Supplier, d.InvoiceNo, d.PhotoUrl, names.GetValueOrDefault(d.CreatedBy) ?? "", d.CreatedAt, d.ConfirmedAt,
                d.AdjustsDocumentId, expenses.TryGetValue(d.Id, out var e) ? e : null,
                d.Type == StockDocumentType.Receipt ? d.Lines.Sum(l => (l.UnitQty ?? l.Qty) * (l.UnitCost ?? 0)) : null,
                d.Lines.OrderBy(l => l.Item.SortOrder).Select(l => new StockLineView(l.ItemId, l.Item.Name, l.Item.BaseUnit, l.Qty, l.UnitName,
                    l.UnitQty, l.UnitCost, l.ExpiryDate, l.SuggestedQty)).ToList());
        }).ToList();
    }

    public static StockDocument NewDocument(
        StockDocumentType type, Guid branchId, Guid? from, Guid? to, Guid? shiftId, CurrentUser user, DateTimeOffset now, string code) => new()
    {
        Code = code, Type = type, BranchId = branchId, FromWarehouseId = from, ToWarehouseId = to, ShiftId = shiftId,
        Status = StockDocumentStatus.Confirmed, CreatedBy = user.UserId, CreatedAt = now, ConfirmedBy = user.UserId, ConfirmedAt = now,
    };

    /// <summary>
    /// Runs a ledger write; a concurrency conflict on a balance (RowVersion / new-row key) or a duplicate code retries
    /// from a clean change tracker, so the retry re-reads stock and may then fail with INSUFFICIENT_STOCK.
    /// </summary>
    private async Task<Result<StockDocumentView>> WithRetryAsync(Func<Task<Guid>> write, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var id = await write();
                db.ChangeTracker.Clear();
                return (await ViewsAsync([id], ct)).Single();
            }
            catch (StockRuleException rule)
            {
                db.ChangeTracker.Clear();
                return rule.Error;
            }
            catch (DbUpdateException) when (attempt < MaxAttempts)
            {
                db.ChangeTracker.Clear();
            }
            catch (DbUpdateException)
            {
                db.ChangeTracker.Clear();
                return InventoryErrors.Retry();
            }
        }
    }

    private async Task<Dictionary<Guid, InventoryItem>?> LoadItemsAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var wanted = ids.Distinct().ToList();
        var items = await db.InventoryItems.AsNoTracking().Include(i => i.Units)
            .Where(i => wanted.Contains(i.Id) && i.IsActive).ToDictionaryAsync(i => i.Id, ct);
        return items.Count == wanted.Count ? items : null;
    }

    private static ServiceError? ValidateLines(IReadOnlyList<QtyLineInput>? lines) =>
        lines is not { Count: > 0 and <= MaxLines } || lines.Select(l => l.ItemId).Distinct().Count() != lines.Count || lines.Any(l => l.Qty <= 0)
            ? ServiceError.Invalid("Lines", $"Cần 1–{MaxLines} dòng, mỗi mặt hàng một dòng, số lượng > 0.")
            : null;

    private static ServiceError CountInProgress() => InventoryErrors.Conflict(InventoryErrors.CountInProgress, "Đang đếm kho ngoài",
        "Ca này đang đếm kho ngoài; hãy xác nhận số đếm trước khi lôi hàng hoặc huỷ hàng ở kho ngoài.");

    private static string MethodLabel(PaymentMethod? method) => method switch
    {
        PaymentMethod.VietQr => "Chuyển khoản",
        PaymentMethod.Cash => "Tiền mặt",
        _ => "",
    };

    private static string Part(string? value) => string.IsNullOrWhiteSpace(value) ? "" : " · " + value;

    private static string Truncate(string value, int max) => value.Length > max ? value[..max] : value;
}

/// <summary>Business-rule failure raised inside a retried ledger write.</summary>
internal sealed class StockRuleException(ServiceError error) : Exception(error.Detail)
{
    public ServiceError Error { get; } = error;
}
