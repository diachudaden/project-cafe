using System.Text.Json;
using ChonRieng.Api.Features.Common;
using ChonRieng.Api.Features.Scheduling;
using ChonRieng.Domain.Entities;
using ChonRieng.Domain.Inventory;
using ChonRieng.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ChonRieng.Api.Features.Inventory;

/// <summary>
/// Stock counts (TASK-027 §4). Shift-close counts of the outer warehouse and weekly inner counts share
/// open → lines → summary → confirm. Blind: nothing returns an expected quantity until every line has a first count.
/// Confirming locks the document, moves the book to the counted quantities and (outer) opens a handover for the next shift.
/// </summary>
public sealed class ShiftCountService(
    AppDbContext db, OuterStockCalculator calculator, InventoryEvents events, IOptions<InventoryOptions> options, TimeProvider clock)
{
    private const int MaxAttempts = 3;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private InventoryOptions Settings => options.Value;

    // ---------- open ----------

    public async Task<Result<CountSheetView>> OpenShiftCountAsync(CurrentUser user, Guid shiftId, CancellationToken ct)
    {
        var shift = await db.Shifts.AsNoTracking().SingleOrDefaultAsync(s => s.Id == shiftId, ct);
        if (shift is null || !user.CanAccessBranch(shift.BranchId))
        {
            return ServiceError.NotFound();
        }

        if (await ShiftAccessAsync(user, shift, ct) is { } denied)
        {
            return denied;
        }

        var (_, outer) = await Warehouses.EnsureAsync(db, shift.BranchId, ct);
        var existing = await LiveShiftCountAsync(shift.Id, outer.Id, ct);
        if (existing is not null)
        {
            return await SheetAsync(existing.Value, ct);
        }

        var items = await CountableItemsAsync(shift.BranchId, shift.Date, outerOnly: true, ct);
        if (items.Count == 0)
        {
            return InventoryErrors.Conflict(InventoryErrors.InvalidState, "Chưa có mặt hàng", "Chi nhánh chưa cấu hình mặt hàng nào cho kho ngoài.");
        }

        var now = clock.GetUtcNow();
        var doc = NewCount(shift.BranchId, outer.Id, shift.Id, user, now, await DocumentCodes.NextAsync(db, StockDocumentType.Count, shift.BranchId, now, ct));
        foreach (var itemId in items)
        {
            doc.Lines.Add(new StockDocumentLine { ItemId = itemId });
        }

        db.StockDocuments.Add(doc);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Another device opened the same shift count first (filtered unique index): resume theirs.
            db.ChangeTracker.Clear();
            existing = await LiveShiftCountAsync(shift.Id, outer.Id, ct);
            if (existing is null)
            {
                return InventoryErrors.Retry();
            }

            return await SheetAsync(existing.Value, ct);
        }

        return await SheetAsync(doc.Id, ct);
    }

    public async Task<Result<CountSheetView>> OpenInnerCountAsync(CurrentUser user, Guid branchId, CancellationToken ct)
    {
        if (!user.CanManageBranch(branchId) || !await db.Branches.AnyAsync(b => b.Id == branchId, ct))
        {
            return ServiceError.NotFound();
        }

        var (inner, _) = await Warehouses.EnsureAsync(db, branchId, ct);
        var draft = await db.StockDocuments.AsNoTracking()
            .Where(d => d.Type == StockDocumentType.Count && d.ToWarehouseId == inner.Id && d.Status == StockDocumentStatus.Draft)
            .Select(d => (Guid?)d.Id).FirstOrDefaultAsync(ct);
        if (draft is { } draftId)
        {
            return await SheetAsync(draftId, ct);
        }

        var now = clock.GetUtcNow();
        var items = await CountableItemsAsync(branchId, VietnamTime.Today(clock), outerOnly: false, ct);
        if (items.Count == 0)
        {
            return InventoryErrors.Conflict(InventoryErrors.InvalidState, "Chưa có mặt hàng", "Chi nhánh chưa cấu hình mặt hàng nào.");
        }

        var doc = NewCount(branchId, inner.Id, null, user, now, await DocumentCodes.NextAsync(db, StockDocumentType.Count, branchId, now, ct));
        foreach (var itemId in items)
        {
            doc.Lines.Add(new StockDocumentLine { ItemId = itemId });
        }

        db.StockDocuments.Add(doc);
        await db.SaveChangesAsync(ct);
        return await SheetAsync(doc.Id, ct);
    }

    // ---------- lines ----------

    /// <summary>Saves counted quantities. The first value of a line is kept forever as <c>FirstCountedQty</c> (blind count).</summary>
    public async Task<Result<CountSheetView>> SaveLinesAsync(CurrentUser user, Guid countId, SaveCountLinesRequest request, CancellationToken ct)
    {
        var doc = await db.StockDocuments.Include(d => d.Lines).Include(d => d.ToWarehouse).Include(d => d.Shift)
            .SingleOrDefaultAsync(d => d.Id == countId && d.Type == StockDocumentType.Count, ct);
        var access = doc is null ? ServiceError.NotFound() : await CountAccessAsync(user, doc, ct);
        if (doc is null || access is { Status: 404 })
        {
            return ServiceError.NotFound();
        }

        if (doc.Status != StockDocumentStatus.Draft)
        {
            return Locked();
        }

        if (access is not null)
        {
            return access;
        }

        if (request.Lines is not { Count: > 0 } || request.Lines.Select(l => l.ItemId).Distinct().Count() != request.Lines.Count
            || request.Lines.Any(l => l.CountedQty < 0))
        {
            return ServiceError.Invalid(nameof(request.Lines), "Mỗi mặt hàng một dòng, số đếm ≥ 0.");
        }

        var lines = doc.Lines.ToDictionary(l => l.ItemId);
        if (request.Lines.Any(l => !lines.ContainsKey(l.ItemId)))
        {
            return ServiceError.Invalid(nameof(request.Lines), "Có mặt hàng không thuộc phiếu đếm này.");
        }

        foreach (var input in request.Lines)
        {
            var line = lines[input.ItemId];
            line.FirstCountedQty ??= input.CountedQty;
            line.CountedQty = input.CountedQty;
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return Locked();
        }

        return await SheetAsync(doc.Id, ct);
    }

    // ---------- summary ----------

    public async Task<Result<CountSummaryView>> SummaryAsync(CurrentUser user, Guid countId, CancellationToken ct)
    {
        var doc = await LoadAsync(countId, tracked: false, ct);
        if (doc is null || await CanViewAsync(user, doc, ct) is false)
        {
            return ServiceError.NotFound();
        }

        if (doc.Lines.Any(l => l.FirstCountedQty is null))
        {
            return Incomplete();
        }

        var rows = doc.Status == StockDocumentStatus.Draft ? await LiveRowsAsync(doc, ct) : SnapshotRows(doc);
        return await SummaryViewAsync(doc, rows, ct);
    }

    // ---------- confirm ----------

    /// <summary>Idempotent: confirming a locked count returns its stored result without writing anything.</summary>
    public async Task<Result<ConfirmCountResult>> ConfirmAsync(CurrentUser user, Guid countId, ConfirmCountRequest request, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            var doc = await LoadAsync(countId, tracked: true, ct);
            var access = doc is null ? ServiceError.NotFound() : await CountAccessAsync(user, doc, ct);
            if (doc is null || access is { Status: 404 })
            {
                return ServiceError.NotFound();
            }

            if (doc.Status != StockDocumentStatus.Draft)
            {
                return StoredResult(doc, alreadyConfirmed: true);
            }

            if (access is not null)
            {
                return access;
            }

            if (doc.Lines.Any(l => l.CountedQty is null))
            {
                return Incomplete();
            }

            var now = clock.GetUtcNow();
            var rows = await LiveRowsAsync(doc, ct, now);
            var outOfTolerance = rows.Where(r => r.WithinTolerance == false).ToList();
            var reason = request.Reason?.Trim();
            if (outOfTolerance.Count > 0 && (reason is null || reason.Length < Settings.CountReasonMinLength))
            {
                return InventoryErrors.Reason($"Có {outOfTolerance.Count} món vượt ngưỡng; lý do cần ít nhất {Settings.CountReasonMinLength} ký tự.");
            }

            // Snapshot the summary on the lines: after this the document is immutable.
            var byItem = rows.ToDictionary(r => r.ItemId);
            foreach (var line in doc.Lines)
            {
                var row = byItem[line.ItemId];
                line.Qty = line.CountedQty!.Value;
                line.OpeningQty = row.Opening;
                line.TransferInQty = row.TransfersIn;
                line.WasteQty = row.Waste;
                line.Norm = row.Norm;
                line.Usage = row.Usage;
                line.ExpectedQty = row.Expected;
                line.Variance = row.Variance;
                line.Tolerance = row.Tolerance;
            }

            var outer = doc.ToWarehouse!.Type == WarehouseType.Outer;
            doc.Status = outer && outOfTolerance.Count > 0 ? StockDocumentStatus.PendingApproval : StockDocumentStatus.Confirmed;
            doc.Reason = string.IsNullOrEmpty(reason) ? null : reason;
            doc.SessionCount = outer ? rows.FirstOrDefault()?.Sessions ?? 0 : null;
            doc.ConfirmedBy = user.UserId;
            doc.ConfirmedAt = now;
            doc.PayloadHash = CountPayload.Hash(
                doc.Lines.Select(l => new CountPayload.Line(l.ItemId, l.CountedQty, l.ExpectedQty)), user.UserId, now);

            // Book stock of the counted warehouse becomes exactly the counted quantity.
            foreach (var line in doc.Lines)
            {
                var balance = await StockLedger.BalanceAsync(db, doc.ToWarehouseId!.Value, line.ItemId, ct);
                await StockLedger.PostAsync(db, doc.ToWarehouseId!.Value, line.ItemId, line.CountedQty!.Value - balance.Qty, doc.Id, now, ct);
            }

            if (outer)
            {
                db.ShiftCountHandovers.Add(new ShiftCountHandover { CountDocumentId = doc.Id, CreatedAt = now });
            }

            if (doc.Status == StockDocumentStatus.PendingApproval)
            {
                await events.CountNeedsApprovalAsync(doc, outOfTolerance.Count, now, ct);
            }

            try
            {
                await db.SaveChangesAsync(ct);
                db.ChangeTracker.Clear();
                return StoredResult((await LoadAsync(countId, tracked: false, ct))!, alreadyConfirmed: false);
            }
            catch (DbUpdateException) when (attempt < MaxAttempts)
            {
                // A parallel confirm won (RowVersion) or the book moved: reload; a locked document returns its stored result.
                db.ChangeTracker.Clear();
            }
            catch (DbUpdateException)
            {
                db.ChangeTracker.Clear();
                return InventoryErrors.Retry();
            }
        }
    }

    // ---------- handover ----------

    /// <summary>Outer counts of the branch still waiting for the next shift (for the POS banner).</summary>
    public async Task<Result<IReadOnlyList<PendingHandoverView>>> PendingHandoversAsync(CurrentUser user, Guid branchId, CancellationToken ct)
    {
        if (!user.CanAccessBranch(branchId))
        {
            return ServiceError.NotFound();
        }

        var myShiftIds = await db.Attendances.AsNoTracking()
            .Where(a => a.Employee.UserId == user.UserId && a.CheckInAt != null && a.CheckOutAt == null && a.Shift.BranchId == branchId)
            .Select(a => a.ShiftId).ToListAsync(ct);
        if (myShiftIds.Count == 0 && !user.CanManageBranch(branchId))
        {
            return Result<IReadOnlyList<PendingHandoverView>>.From([]);
        }

        var rows = await db.ShiftCountHandovers.AsNoTracking()
            .Where(h => h.Status == HandoverStatus.Waiting && h.CountDocument.BranchId == branchId && h.CountDocument.ShiftId != null
                && !myShiftIds.Contains(h.CountDocument.ShiftId!.Value))
            .OrderByDescending(h => h.CreatedAt)
            .Select(h => new
            {
                h.CountDocumentId, h.CountDocument.Code, ShiftId = h.CountDocument.ShiftId!.Value, h.CountDocument.Shift!.Type,
                h.CountDocument.Shift.StartAt, h.CountDocument.Shift.EndAt, h.Status, CountStatus = h.CountDocument.Status,
                h.CountDocument.ConfirmedBy, ConfirmedAt = h.CountDocument.ConfirmedAt!.Value,
            })
            .Take(5).ToListAsync(ct);
        var names = await NamesAsync(rows.Select(r => r.ConfirmedBy), ct);
        return Result<IReadOnlyList<PendingHandoverView>>.From(rows.Select(r => new PendingHandoverView(
            r.CountDocumentId, r.Code, r.ShiftId, r.Type, r.StartAt, r.EndAt, r.Status, r.CountStatus,
            names.GetValueOrDefault(r.ConfirmedBy ?? Guid.Empty) ?? "", r.ConfirmedAt)).ToList());
    }

    public async Task<Result<CountSummaryView>> AcceptAsync(CurrentUser user, Guid countId, CancellationToken ct)
    {
        var (handover, error) = await ReceivableAsync(user, countId, ct);
        if (error is not null)
        {
            return error;
        }

        if (handover!.Status == HandoverStatus.Waiting)
        {
            handover.Status = HandoverStatus.Accepted;
            handover.ReceivedBy = user.UserId;
            handover.ReceivedAt = clock.GetUtcNow();
            handover.ReceivedShiftId = await MyShiftAtAsync(user, handover.CountDocument.BranchId, ct);
            if (await SaveHandoverAsync(ct) is { } conflict)
            {
                return conflict;
            }
        }
        else if (handover.Status != HandoverStatus.Accepted)
        {
            return HandoverClosed(handover.Status);
        }

        return await SummaryAsync(user, countId, ct);
    }

    public async Task<Result<CountSummaryView>> DisputeAsync(CurrentUser user, Guid countId, DisputeHandoverRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Note))
        {
            return InventoryErrors.Reason("Cần ghi chú khi báo không khớp.");
        }

        var (handover, error) = await ReceivableAsync(user, countId, ct);
        if (error is not null)
        {
            return error;
        }

        if (handover!.Status != HandoverStatus.Waiting)
        {
            return handover.Status == HandoverStatus.Disputed ? await SummaryAsync(user, countId, ct) : HandoverClosed(handover.Status);
        }

        var itemIds = await db.StockDocumentLines.Where(l => l.DocumentId == countId).Select(l => l.ItemId).ToListAsync(ct);
        if (request.Lines is not { Count: > 0 } || request.Lines.Any(l => !itemIds.Contains(l.ItemId) || l.CountedQty < 0)
            || request.Lines.Select(l => l.ItemId).Distinct().Count() != request.Lines.Count)
        {
            return ServiceError.Invalid(nameof(request.Lines), "Chọn ít nhất một món của phiếu và nhập số tự đếm (≥ 0).");
        }

        var now = clock.GetUtcNow();
        handover.Status = HandoverStatus.Disputed;
        handover.ReceivedBy = user.UserId;
        handover.ReceivedAt = now;
        handover.ReceivedShiftId = await MyShiftAtAsync(user, handover.CountDocument.BranchId, ct);
        handover.DisputeNote = request.Note.Trim();
        handover.DisputeLines = JsonSerializer.Serialize(request.Lines.Select(l => new HandoverLine(l.ItemId, l.CountedQty)), Json);
        await events.HandoverDisputedAsync(handover.CountDocument, request.Lines.Count, now, ct);
        if (await SaveHandoverAsync(ct) is { } conflict)
        {
            return conflict;
        }

        return await SummaryAsync(user, countId, ct);
    }

    /// <summary>
    /// Manager settles a disputed (or waiting) handover. The difference to the count is posted as an adjustment (DC)
    /// that adjusts the count, so the next shift's opening becomes the final quantity. The count itself stays untouched.
    /// </summary>
    public async Task<Result<CountSummaryView>> ResolveAsync(CurrentUser user, Guid countId, ResolveHandoverRequest request, CancellationToken ct)
    {
        var handover = await db.ShiftCountHandovers.Include(h => h.CountDocument).ThenInclude(d => d.Lines)
            .SingleOrDefaultAsync(h => h.CountDocumentId == countId, ct);
        if (handover is null || !user.CanManageBranch(handover.CountDocument.BranchId))
        {
            return ServiceError.NotFound();
        }

        if (handover.Status is HandoverStatus.Resolved or HandoverStatus.Accepted)
        {
            return HandoverClosed(handover.Status);
        }

        if (string.IsNullOrWhiteSpace(request.Note))
        {
            return InventoryErrors.Reason("Cần ghi chú khi chốt số.");
        }

        var count = handover.CountDocument;
        var lines = count.Lines.ToDictionary(l => l.ItemId);
        if (request.Lines is not { Count: > 0 } || request.Lines.Any(l => !lines.ContainsKey(l.ItemId) || l.FinalQty < 0)
            || request.Lines.Select(l => l.ItemId).Distinct().Count() != request.Lines.Count)
        {
            return ServiceError.Invalid(nameof(request.Lines), "Mỗi món của phiếu một dòng, số chốt ≥ 0.");
        }

        var now = clock.GetUtcNow();
        var deltas = request.Lines.Select(l => (l.ItemId, Delta: l.FinalQty - lines[l.ItemId].CountedQty!.Value)).Where(x => x.Delta != 0).ToList();
        if (deltas.Count > 0)
        {
            // A later outer count already reset the book; then the adjustment is recorded without moving stock again.
            var superseded = await db.StockDocuments.AnyAsync(d => d.Type == StockDocumentType.Count && d.ToWarehouseId == count.ToWarehouseId
                && d.Id != count.Id && d.ConfirmedAt > count.ConfirmedAt
                && (d.Status == StockDocumentStatus.Confirmed || d.Status == StockDocumentStatus.PendingApproval || d.Status == StockDocumentStatus.Approved), ct);
            var adjustment = StockDocumentService.NewDocument(StockDocumentType.Adjustment, count.BranchId, null, count.ToWarehouseId, count.ShiftId, user, now,
                await DocumentCodes.NextAsync(db, StockDocumentType.Adjustment, count.BranchId, now, ct));
            adjustment.AdjustsDocumentId = count.Id;
            adjustment.Reason = $"Chốt bàn giao {count.Code}: {request.Note.Trim()}";
            adjustment.Note = superseded ? "Không ghi sổ: ca sau đã kiểm kê lại kho ngoài." : null;
            foreach (var (itemId, delta) in deltas)
            {
                adjustment.Lines.Add(new StockDocumentLine { ItemId = itemId, Qty = delta });
                if (!superseded)
                {
                    await StockLedger.PostAsync(db, count.ToWarehouseId!.Value, itemId, delta, adjustment.Id, now, ct);
                }
            }

            db.StockDocuments.Add(adjustment);
            handover.ResolveAdjustmentId = adjustment.Id;
        }

        handover.Status = HandoverStatus.Resolved;
        handover.ResolvedBy = user.UserId;
        handover.ResolvedAt = now;
        handover.ResolveNote = request.Note.Trim();
        if (await SaveHandoverAsync(ct) is { } conflict)
        {
            return conflict;
        }

        return await SummaryAsync(user, countId, ct);
    }

    // ---------- helpers ----------

    private async Task<(ShiftCountHandover?, ServiceError?)> ReceivableAsync(CurrentUser user, Guid countId, CancellationToken ct)
    {
        var handover = await db.ShiftCountHandovers.Include(h => h.CountDocument)
            .SingleOrDefaultAsync(h => h.CountDocumentId == countId, ct);
        if (handover is null || !user.CanAccessBranch(handover.CountDocument.BranchId))
        {
            return (null, ServiceError.NotFound());
        }

        var myShift = await MyShiftAtAsync(user, handover.CountDocument.BranchId, ct);
        if (myShift == handover.CountDocument.ShiftId && myShift is not null)
        {
            return (null, InventoryErrors.Conflict(InventoryErrors.NotYourShift, "Không thể tự nhận bàn giao", "Ca sau mới nhận bàn giao số đếm của ca này."));
        }

        if (myShift is null && !user.CanManageBranch(handover.CountDocument.BranchId))
        {
            return (null, InventoryErrors.NoShift());
        }

        return (handover, null);
    }

    private async Task<ServiceError?> SaveHandoverAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return null;
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return InventoryErrors.Conflict(InventoryErrors.InvalidState, "Bàn giao vừa được cập nhật", "Người khác vừa xử lý bàn giao này, hãy tải lại.");
        }
    }

    private Task<Guid?> MyShiftAtAsync(CurrentUser user, Guid branchId, CancellationToken ct) =>
        db.Attendances.Where(a => a.Employee.UserId == user.UserId && a.CheckInAt != null && a.CheckOutAt == null && a.Shift.BranchId == branchId)
            .OrderByDescending(a => a.CheckInAt).Select(a => (Guid?)a.ShiftId).FirstOrDefaultAsync(ct);

    /// <summary>Count shift staff (checked in, not out) or a manager of the branch.</summary>
    private async Task<ServiceError?> ShiftAccessAsync(CurrentUser user, Shift shift, CancellationToken ct)
    {
        if (user.CanManageBranch(shift.BranchId))
        {
            return null;
        }

        var mine = await db.Attendances.AnyAsync(a => a.ShiftId == shift.Id && a.Employee.UserId == user.UserId && a.CheckInAt != null && a.CheckOutAt == null, ct);
        if (mine)
        {
            return null;
        }

        var inAnyShift = await db.Attendances.AnyAsync(a => a.Employee.UserId == user.UserId && a.CheckInAt != null && a.CheckOutAt == null, ct);
        return inAnyShift
            ? InventoryErrors.Conflict(InventoryErrors.NotYourShift, "Không phải ca của bạn", "Chỉ nhân viên đang trong ca này mới được đếm kết ca.")
            : InventoryErrors.NoShift();
    }

    /// <summary>404 when out of scope; 409 when in scope but not allowed to write.</summary>
    private async Task<ServiceError?> CountAccessAsync(CurrentUser user, StockDocument doc, CancellationToken ct)
    {
        if (!user.CanAccessBranch(doc.BranchId))
        {
            return ServiceError.NotFound();
        }

        if (doc.ShiftId is null)
        {
            return user.CanManageBranch(doc.BranchId) ? null : ServiceError.NotFound();
        }

        var shift = doc.Shift ?? await db.Shifts.AsNoTracking().SingleAsync(s => s.Id == doc.ShiftId, ct);
        return await ShiftAccessAsync(user, shift, ct);
    }

    private async Task<bool> CanViewAsync(CurrentUser user, StockDocument doc, CancellationToken ct)
    {
        if (!user.CanAccessBranch(doc.BranchId))
        {
            return false;
        }

        if (user.CanManageBranch(doc.BranchId))
        {
            return true;
        }

        if (doc.ShiftId is null)
        {
            return false;
        }

        if (await db.Attendances.AnyAsync(a => a.ShiftId == doc.ShiftId && a.Employee.UserId == user.UserId, ct))
        {
            return true;
        }

        // The next shift reads a locked count to receive the handover.
        return doc.Status != StockDocumentStatus.Draft && await MyShiftAtAsync(user, doc.BranchId, ct) is not null;
    }

    private Task<Guid?> LiveShiftCountAsync(Guid shiftId, Guid outerId, CancellationToken ct) =>
        db.StockDocuments.AsNoTracking()
            .Where(d => d.Type == StockDocumentType.Count && d.ShiftId == shiftId && d.ToWarehouseId == outerId && d.Status != StockDocumentStatus.Rejected)
            .Select(d => (Guid?)d.Id).FirstOrDefaultAsync(ct);

    private async Task<List<Guid>> CountableItemsAsync(Guid branchId, DateOnly date, bool outerOnly, CancellationToken ct)
    {
        var items = await db.ItemBranchSettings.AsNoTracking()
            .Where(s => s.BranchId == branchId && s.Item.IsActive)
            .OrderBy(s => s.Item.SortOrder).ThenBy(s => s.Item.Name)
            .Select(s => new { s.ItemId, s.Item.CountDaily, s.Item.SortOrder })
            .ToListAsync(ct);
        return items.Where(i => !outerOnly || i.CountDaily || InventoryMath.IsRotationDay(date, i.SortOrder, Settings.RotationDays))
            .Select(i => i.ItemId).ToList();
    }

    private static StockDocument NewCount(Guid branchId, Guid warehouseId, Guid? shiftId, CurrentUser user, DateTimeOffset now, string code) => new()
    {
        Code = code, Type = StockDocumentType.Count, BranchId = branchId, ToWarehouseId = warehouseId, ShiftId = shiftId,
        Status = StockDocumentStatus.Draft, CreatedBy = user.UserId, CreatedAt = now,
    };

    private Task<StockDocument?> LoadAsync(Guid countId, bool tracked, CancellationToken ct)
    {
        var query = db.StockDocuments.Include(d => d.Lines).ThenInclude(l => l.Item).ThenInclude(i => i.Units)
            .Include(d => d.ToWarehouse).Include(d => d.Shift).Include(d => d.Branch)
            .AsSplitQuery()
            .Where(d => d.Id == countId && d.Type == StockDocumentType.Count);
        return (tracked ? query : query.AsNoTracking()).SingleOrDefaultAsync(ct);
    }

    private async Task<Result<CountSheetView>> SheetAsync(Guid countId, CancellationToken ct)
    {
        var doc = (await LoadAsync(countId, tracked: false, ct))!;
        return new CountSheetView(doc.Id, doc.Code, doc.ToWarehouse!.Type, doc.ShiftId, doc.Status,
            doc.Lines.OrderBy(l => l.Item.SortOrder).ThenBy(l => l.Item.Name)
                .Select(l => new CountItemView(l.ItemId, l.Item.Name, l.Item.BaseUnit, Units(l.Item), l.Item.SortOrder, l.CountedQty)).ToList());
    }

    /// <summary>Live figures for a draft count: outer from the calculator; inner against the exact book.</summary>
    private async Task<List<CountSummaryRow>> LiveRowsAsync(StockDocument doc, CancellationToken ct, DateTimeOffset? at = null)
    {
        var now = at ?? clock.GetUtcNow();
        var lines = doc.Lines.OrderBy(l => l.Item.SortOrder).ThenBy(l => l.Item.Name).ToList();
        if (doc.ToWarehouse!.Type == WarehouseType.Inner)
        {
            var book = await db.StockBalances.AsNoTracking().Where(b => b.WarehouseId == doc.ToWarehouseId)
                .ToDictionaryAsync(b => b.ItemId, b => b.Qty, ct);
            return lines.Select(l =>
            {
                var expected = book.GetValueOrDefault(l.ItemId);
                var variance = l.CountedQty - expected;
                return new CountSummaryRow(l.ItemId, l.Item.Name, l.Item.BaseUnit, Units(l.Item), expected, 0, 0, 0, 0, 0, expected,
                    l.FirstCountedQty, l.CountedQty, variance, 0, variance is null ? null : variance == 0);
            }).ToList();
        }

        var figures = await calculator.ComputeAsync(doc.BranchId, doc.ToWarehouseId!.Value, doc.Shift, now, ct);
        return lines.Select(l =>
        {
            var f = figures.GetValueOrDefault(l.ItemId) ?? new OuterFigures(l.ItemId, 0, 0, 0, 0, 0, 0, 0);
            var r = InventoryMath.EvaluateShiftLine(f.Opening, f.TransfersIn, f.Waste, f.Sessions, f.Norm, l.CountedQty,
                Settings.ToleranceMinUnits, Settings.TolerancePercent);
            return new CountSummaryRow(l.ItemId, l.Item.Name, l.Item.BaseUnit, Units(l.Item), r.Opening, r.TransfersIn, r.Waste, r.Sessions,
                r.Norm, r.Usage, r.Expected, l.FirstCountedQty, l.CountedQty, r.Variance, r.Tolerance, r.WithinTolerance);
        }).ToList();
    }

    private static List<CountSummaryRow> SnapshotRows(StockDocument doc) =>
        doc.Lines.OrderBy(l => l.Item.SortOrder).ThenBy(l => l.Item.Name).Select(l => new CountSummaryRow(
            l.ItemId, l.Item.Name, l.Item.BaseUnit, Units(l.Item), l.OpeningQty ?? 0, l.TransferInQty ?? 0, l.WasteQty ?? 0,
            doc.SessionCount ?? 0, l.Norm ?? 0, l.Usage ?? 0, l.ExpectedQty ?? 0, l.FirstCountedQty, l.CountedQty, l.Variance,
            l.Tolerance ?? 0, l.Variance is null ? null : Math.Abs(l.Variance.Value) <= (l.Tolerance ?? 0))).ToList();

    private async Task<CountSummaryView> SummaryViewAsync(StockDocument doc, List<CountSummaryRow> rows, CancellationToken ct)
    {
        var handover = await db.ShiftCountHandovers.AsNoTracking().SingleOrDefaultAsync(h => h.CountDocumentId == doc.Id, ct);
        var names = await NamesAsync([doc.CreatedBy, doc.ConfirmedBy, handover?.ReceivedBy, handover?.ResolvedBy], ct);
        string? Name(Guid? id) => id is { } x ? names.GetValueOrDefault(x) : null;
        var info = handover is null ? null : new HandoverInfo(handover.Id, handover.Status, Name(handover.ReceivedBy), handover.ReceivedAt,
            handover.DisputeNote, handover.DisputeLines is null ? [] : JsonSerializer.Deserialize<List<HandoverLine>>(handover.DisputeLines, Json)!,
            Name(handover.ResolvedBy), handover.ResolvedAt, handover.ResolveNote);
        return new CountSummaryView(doc.Id, doc.Code, doc.ToWarehouse!.Type, doc.Status, doc.BranchId, doc.Branch.Name,
            doc.ShiftId, doc.Shift?.Type, doc.Shift?.StartAt, doc.Shift?.EndAt, Name(doc.CreatedBy) ?? "", Name(doc.ConfirmedBy), doc.ConfirmedAt,
            rows.FirstOrDefault()?.Sessions ?? 0, doc.Reason, doc.ApprovalNote,
            rows.Count(r => r.WithinTolerance == true), rows.Count, rows, info, doc.PayloadHash);
    }

    private static ConfirmCountResult StoredResult(StockDocument doc, bool alreadyConfirmed) =>
        new(doc.Id, doc.Status, alreadyConfirmed, doc.Lines
            .Where(l => l.Variance is { } v && Math.Abs(v) > (l.Tolerance ?? 0))
            .OrderBy(l => l.Item.SortOrder)
            .Select(l => new CountedOutOfTolerance(l.ItemId, l.Item.Name, l.Item.BaseUnit, l.ExpectedQty ?? 0, l.CountedQty ?? 0, l.Variance!.Value, l.Tolerance ?? 0))
            .ToList());

    private async Task<Dictionary<Guid, string>> NamesAsync(IEnumerable<Guid?> ids, CancellationToken ct)
    {
        var wanted = ids.Where(i => i is not null).Select(i => i!.Value).Distinct().ToList();
        return await db.Users.AsNoTracking().Where(u => wanted.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
    }

    private static List<ItemUnitView> Units(InventoryItem item) =>
        item.Units.OrderBy(u => u.Factor).Select(u => new ItemUnitView(u.UnitName, u.Factor)).ToList();

    private static ServiceError Locked() => InventoryErrors.Conflict(InventoryErrors.CountAlreadyConfirmed, "Phiếu đếm đã khoá",
        "Số đếm đã được xác nhận và không sửa được. Sai thì quản lý lập phiếu điều chỉnh.");

    private static ServiceError Incomplete() => InventoryErrors.Conflict(InventoryErrors.CountIncomplete, "Chưa đếm xong",
        "Hãy nhập số đếm cho tất cả mặt hàng trước khi xem tổng hợp.");

    private static ServiceError HandoverClosed(HandoverStatus status) => InventoryErrors.Conflict(InventoryErrors.InvalidState, "Bàn giao đã xử lý",
        status switch
        {
            HandoverStatus.Accepted => "Ca sau đã xác nhận nhận bàn giao.",
            HandoverStatus.Disputed => "Ca sau đã báo không khớp; chờ quản lý chốt số.",
            _ => "Quản lý đã chốt số cho bàn giao này.",
        });
}
