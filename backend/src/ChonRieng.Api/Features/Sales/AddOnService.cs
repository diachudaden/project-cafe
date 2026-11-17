using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using ChonRieng.Api.Features.Common;
using ChonRieng.Api.Features.Engagement;
using ChonRieng.Domain.Entities;
using ChonRieng.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChonRieng.Api.Features.Sales;

public sealed record ProductView(
    Guid Id,
    [property: Description("null = áp dụng toàn chuỗi.")] Guid? BranchId,
    string Name,
    string Category,
    decimal Price,
    bool IsActive,
    int SortOrder);

public sealed record ProductRequest(
    [property: Description("null = toàn chuỗi (chỉ Admin).")] Guid? BranchId,
    [property: Required, MaxLength(100)] string Name,
    [property: Required, MaxLength(50), Description("Nhóm hiển thị trên POS, vd: Đồ ăn, Đồ uống.")] string Category,
    [property: Range(0, 10_000_000)] decimal Price,
    bool IsActive,
    int SortOrder);

public sealed record AddOnLineRequest(Guid ProductId, [property: Range(1, 99)] int Quantity);

public sealed record AddOnSaleResponse(
    IReadOnlyList<BillItemView> Items,
    string BillCode,
    [property: Description("Tổng hóa đơn sau khi thêm đồ.")] decimal BillTotal,
    [property: Description("Điểm thành viên được cộng cho lần bán này (0 nếu khách lẻ).")] int PointsAwarded,
    [property: Description("Tên thành viên được cộng điểm, null nếu khách lẻ.")] string? CustomerName);

public sealed record AddOnSaleRequest([property: Required, MinLength(1), MaxLength(AddOnService.MaxLines)] IReadOnlyList<AddOnLineRequest> Items);

public sealed record BillItemView(Guid Id, string Name, decimal UnitPrice, int Quantity, decimal Amount, PaymentMethod PaymentMethod, DateTimeOffset CreatedAt);

/// <summary>A priced line, snapshotted at sale / payment-request time.</summary>
public sealed record AddOnLine(Guid ProductId, string Name, decimal UnitPrice, int Quantity)
{
    public decimal Amount => UnitPrice * Quantity;
}

/// <summary>Add-on items (đồ thêm) sold on an active session's bill, and the menu they come from.</summary>
public sealed class AddOnService(AppDbContext db, TimeProvider clock)
{
    /// <summary>Max add-on lines per sale / check-in (TASK-026).</summary>
    public const int MaxLines = 30;

    /// <summary>Length of <c>PaymentRequests.Items</c> (nvarchar(4000)).</summary>
    public const int MaxSnapshotLength = 4000;

    // Vietnamese stays unescaped (1 char instead of \uXXXX) so a 30-line snapshot fits the column; reading is unchanged.
    private static readonly JsonSerializerOptions LineJson = new(JsonSerializerDefaults.Web)
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    // ---- Menu ----

    /// <summary>Active products the POS can sell at the branch, chain-wide ones included.</summary>
    public async Task<Result<IReadOnlyList<ProductView>>> MenuAsync(CurrentUser user, Guid branchId, CancellationToken ct)
    {
        if (!user.CanAccessBranch(branchId))
        {
            return ServiceError.NotFound();
        }

        return Result<IReadOnlyList<ProductView>>.From(await Products(branchId, activeOnly: true).ToListAsync(ct));
    }

    /// <summary>All products (inactive too) for the management screen.</summary>
    public async Task<Result<IReadOnlyList<ProductView>>> ManagedAsync(CurrentUser user, Guid branchId, CancellationToken ct)
    {
        if (!user.CanManageBranch(branchId))
        {
            return ServiceError.NotFound();
        }

        return Result<IReadOnlyList<ProductView>>.From(await Products(branchId).ToListAsync(ct));
    }

    public async Task<Result<ProductView>> CreateAsync(CurrentUser user, ProductRequest request, CancellationToken ct)
    {
        var error = await ValidateAsync(user, request, ct);
        if (error is not null)
        {
            return error;
        }

        var product = new Product { CreatedAt = clock.GetUtcNow() };
        Apply(product, request);
        db.Products.Add(product);
        await db.SaveChangesAsync(ct);
        return ToView(product);
    }

    public async Task<Result<ProductView>> UpdateAsync(CurrentUser user, Guid productId, ProductRequest request, CancellationToken ct)
    {
        var product = await db.Products.SingleOrDefaultAsync(p => p.Id == productId, ct);
        if (product is null || !CanManage(user, product.BranchId))
        {
            return ServiceError.NotFound();
        }

        var error = await ValidateAsync(user, request, ct);
        if (error is not null)
        {
            return error;
        }

        Apply(product, request);
        await db.SaveChangesAsync(ct);
        return ToView(product);
    }

    /// <summary>Deletes a never-sold product; a product already on a bill is only deactivated.</summary>
    public async Task<Result<Done>> DeleteAsync(CurrentUser user, Guid productId, CancellationToken ct)
    {
        var product = await db.Products.SingleOrDefaultAsync(p => p.Id == productId, ct);
        if (product is null || !CanManage(user, product.BranchId))
        {
            return ServiceError.NotFound();
        }

        if (await db.BillItems.AnyAsync(i => i.ProductId == productId, ct))
        {
            product.IsActive = false;
        }
        else
        {
            db.Products.Remove(product);
        }

        await db.SaveChangesAsync(ct);
        return new Done();
    }

    // ---- Selling ----

    /// <summary>Cash sale: lines go on the session's bill immediately.</summary>
    public async Task<Result<AddOnSaleResponse>> SellCashAsync(CurrentUser user, Guid sessionId, AddOnSaleRequest request, CancellationToken ct)
    {
        var session = await ActiveSessionAsync(user, sessionId, ct);
        if (session.Error is not null)
        {
            return session.Error;
        }

        var lines = await PriceLinesAsync(session.Value!.Seat.Zone.BranchId, request, ct);
        if (lines.Error is not null)
        {
            return lines.Error;
        }

        var sale = await AddToBillAsync(session.Value.Bill!, session.Value.Seat.Code, lines.Value!, PaymentMethod.Cash, null, user.UserId, clock.GetUtcNow(), ct);
        // Include the session row in the save so its Status/EndAt concurrency tokens are checked: a seat
        // auto-released a moment ago (TASK-024) must not receive new items.
        db.Entry(session.Value).Property(s => s.Status).IsModified = true;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return SessionService.SessionChanged();
        }

        var bill = session.Value.Bill!;
        return new AddOnSaleResponse(sale.Items.Select(ToView).ToList(), bill.Code, bill.Total, sale.PointsAwarded, sale.CustomerName);
    }

    /// <summary>VietQR: computes the amount server-side and snapshots the lines on the payment request.</summary>
    public async Task<Result<PaymentRequestContext>> PreparePaymentAsync(CurrentUser user, Guid sessionId, AddOnSaleRequest request, CancellationToken ct)
    {
        var session = await ActiveSessionAsync(user, sessionId, ct);
        if (session.Error is not null)
        {
            return session.Error;
        }

        var s = session.Value!;
        if (await db.PaymentRequests.AnyAsync(r => r.SeatId == s.SeatId && r.Status == PaymentRequestStatus.Pending && r.ExpiresAt > clock.GetUtcNow(), ct))
        {
            return ServiceError.Conflict("Đang có yêu cầu thanh toán", "Hãy hoàn tất hoặc hủy yêu cầu đang chờ trước khi tạo yêu cầu khác.");
        }

        var lines = await PriceLinesAsync(s.Seat.Zone.BranchId, request, ct);
        if (lines.Error is not null)
        {
            return lines.Error;
        }

        var snapshot = Snapshot(lines.Value!);
        if (snapshot.Error is not null)
        {
            return snapshot.Error;
        }

        return new PaymentRequestContext(PaymentRequestKind.AddOn, s.Seat.Zone.BranchId, s.SeatId, s.Seat.Type, s.PricePlanId, s.CustomerId, s.Id,
            s.Bill!.ShiftId, lines.Value!.Sum(l => l.Amount), snapshot.Value);
    }

    /// <summary>Called when SePay confirms an AddOn request; amounts come from the snapshot taken at request time.</summary>
    public async Task<PaymentSettlementOutcome> SettleAsync(PaymentRequest paymentRequest, CancellationToken ct)
    {
        if (!TryReadSnapshot(paymentRequest.Items, out var lines) || lines.Count == 0 || lines.Sum(l => l.Amount) != paymentRequest.Amount)
        {
            return PaymentSettlementOutcome.NeedsAttention("Danh sách đồ thêm của yêu cầu không hợp lệ.");
        }

        var session = await db.Sessions.Include(s => s.Seat).Include(s => s.Bill)
            .SingleOrDefaultAsync(s => s.Id == paymentRequest.SessionId, ct);
        if (session?.Bill is null || session.Status != SessionStatus.Active)
        {
            return PaymentSettlementOutcome.NeedsAttention(SessionService.EndedReason(session));
        }

        // One timestamp for the lines, the points and PaidAt, so the POS can find this payment's points.
        var now = clock.GetUtcNow();
        await AddToBillAsync(session.Bill, session.Seat.Code, lines, PaymentMethod.VietQr, paymentRequest.Id, paymentRequest.CreatedByUserId, now, ct);
        paymentRequest.BillId = session.Bill.Id;
        paymentRequest.PaidAt = now;
        return new PaymentSettlementOutcome(true, null, session.Bill.Id, session.Bill.Code, session.Seat.Code, session.Id);
    }

    private sealed record AddedToBill(List<BillItem> Items, int PointsAwarded, string? CustomerName);

    private async Task<AddedToBill> AddToBillAsync(
        Bill bill, string seatCode, IReadOnlyList<AddOnLine> lines, PaymentMethod method, Guid? paymentRequestId, Guid? userId,
        DateTimeOffset now, CancellationToken ct)
    {
        var items = NewItems(bill, lines, method, paymentRequestId, userId, now);
        db.BillItems.AddRange(items);

        var total = items.Sum(i => i.Amount);
        bill.Total += total;
        if (method == PaymentMethod.VietQr)
        {
            bill.VietQrPaidTotal += total;
        }
        else
        {
            bill.CashPaidTotal += total;
        }

        bill.PaymentMethod = bill.CashPaidTotal > 0 && bill.VietQrPaidTotal > 0 ? PaymentMethod.Mixed
            : bill.VietQrPaidTotal > 0 ? PaymentMethod.VietQr : PaymentMethod.Cash;

        if (bill.ClaimedByCustomerId is { } customerId && total > 0)
        {
            var customer = await db.Customers.Include(c => c.User).SingleAsync(c => c.Id == customerId, ct);
            var tx = Loyalty.Award(customer, bill, total, now, $"Đồ thêm hóa đơn {bill.Code}");
            db.PointTransactions.Add(tx);
            db.PaymentCompleted(customer, bill, seatCode, PaymentKind.AddOn, total, method, tx.Points, now);
            return new AddedToBill(items, tx.Points, customer.User.FullName);
        }

        return new AddedToBill(items, 0, null);
    }

    private Task<Result<IReadOnlyList<AddOnLine>>> PriceLinesAsync(Guid branchId, AddOnSaleRequest request, CancellationToken ct) =>
        PriceLinesAsync(branchId, request.Items, required: true, ct);

    /// <summary>
    /// Prices the requested lines from the current menu: active, sold at this branch (or chain-wide), at most
    /// <see cref="MaxLines"/> lines, quantities 1–99. Prices always come from the database, never from the client.
    /// With <paramref name="required"/> false, no items means an empty list (check-in without add-ons).
    /// </summary>
    internal async Task<Result<IReadOnlyList<AddOnLine>>> PriceLinesAsync(
        Guid branchId, IReadOnlyList<AddOnLineRequest>? items, bool required, CancellationToken ct)
    {
        if (items is not { Count: > 0 })
        {
            return required
                ? ServiceError.Invalid("Items", "Chọn ít nhất một món.")
                : Result<IReadOnlyList<AddOnLine>>.From([]);
        }

        if (items.Count > MaxLines)
        {
            return ServiceError.Invalid("Items", $"Tối đa {MaxLines} dòng món mỗi lần.");
        }

        if (items.Any(i => i.Quantity is < 1 or > 99))
        {
            return ServiceError.Invalid("Items", "Số lượng mỗi món từ 1 đến 99.");
        }

        var wanted = items.GroupBy(i => i.ProductId).Select(g => (ProductId: g.Key, Quantity: g.Sum(i => i.Quantity))).ToList();
        var ids = wanted.Select(w => w.ProductId).ToList();
        var products = await db.Products.AsNoTracking()
            .Where(p => ids.Contains(p.Id) && p.IsActive && (p.BranchId == null || p.BranchId == branchId))
            .ToDictionaryAsync(p => p.Id, ct);
        if (products.Count != ids.Count)
        {
            return ServiceError.Invalid("Items", "Có món không còn bán tại chi nhánh này.");
        }

        if (wanted.Any(w => w.Quantity > 99))
        {
            return ServiceError.Invalid("Items", "Số lượng mỗi món tối đa 99.");
        }

        return Result<IReadOnlyList<AddOnLine>>.From(wanted
            .Select(w => new AddOnLine(w.ProductId, products[w.ProductId].Name, products[w.ProductId].Price, w.Quantity))
            .ToList());
    }

    private async Task<Result<Session>> ActiveSessionAsync(CurrentUser user, Guid sessionId, CancellationToken ct)
    {
        var session = await db.Sessions.Include(s => s.Seat).ThenInclude(s => s.Zone).Include(s => s.Bill)
            .SingleOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session is null || !user.CanAccessBranch(session.Seat.Zone.BranchId))
        {
            return ServiceError.NotFound();
        }

        if (session.Status != SessionStatus.Active || session.Bill is null)
        {
            return ServiceError.Conflict("Lượt ngồi đã kết thúc", "Chỉ thêm đồ cho lượt đang diễn ra.");
        }

        return session;
    }

    // Filters must run on Product, before the projection: EF Core cannot translate a predicate on a
    // constructor-built ProductView (that was the POS menu 500 on SQL Server, TASK-026).
    private IQueryable<ProductView> Products(Guid branchId, bool activeOnly = false) =>
        db.Products.AsNoTracking()
            .Where(p => p.BranchId == null || p.BranchId == branchId)
            .Where(p => !activeOnly || p.IsActive)
            .OrderBy(p => p.Category).ThenBy(p => p.SortOrder).ThenBy(p => p.Name)
            .Select(p => new ProductView(p.Id, p.BranchId, p.Name, p.Category, p.Price, p.IsActive, p.SortOrder));

    private async Task<ServiceError?> ValidateAsync(CurrentUser user, ProductRequest request, CancellationToken ct)
    {
        if (!CanManage(user, request.BranchId))
        {
            return request.BranchId is null
                ? ServiceError.Forbidden("Chỉ Admin tạo được món áp dụng toàn chuỗi.")
                : ServiceError.NotFound();
        }

        if (request.BranchId is { } id && !await db.Branches.AnyAsync(b => b.Id == id, ct))
        {
            return ServiceError.Invalid("BranchId", "Chi nhánh không tồn tại.");
        }

        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Category))
        {
            return ServiceError.Invalid("Name", "Nhập tên món và nhóm.");
        }

        return request.Price < 0 ? ServiceError.Invalid("Price", "Giá không được âm.") : null;
    }

    private static bool CanManage(CurrentUser user, Guid? branchId) =>
        branchId is { } id ? user.CanManageBranch(id) : user.IsAdmin;

    private static void Apply(Product product, ProductRequest r)
    {
        product.BranchId = r.BranchId;
        product.Name = r.Name.Trim();
        product.Category = r.Category.Trim();
        product.Price = r.Price;
        product.IsActive = r.IsActive;
        product.SortOrder = r.SortOrder;
    }

    /// <summary>Bill lines for already priced add-ons (no points: the caller awards them once for its total).</summary>
    internal static List<BillItem> NewItems(
        Bill bill, IEnumerable<AddOnLine> lines, PaymentMethod method, Guid? paymentRequestId, Guid? userId, DateTimeOffset now) =>
        lines.Select(l => new BillItem
        {
            BillId = bill.Id,
            ProductId = l.ProductId,
            Name = l.Name,
            UnitPrice = l.UnitPrice,
            Quantity = l.Quantity,
            Amount = l.Amount,
            PaymentMethod = method,
            PaymentRequestId = paymentRequestId,
            CreatedByUserId = userId,
            CreatedAt = now,
        }).ToList();

    /// <summary>JSON snapshot stored on a payment request; null for no lines; 400 if it would not fit the column.</summary>
    internal static Result<string?> Snapshot(IReadOnlyList<AddOnLine> lines)
    {
        if (lines.Count == 0)
        {
            return Result<string?>.From(null);
        }

        var json = JsonSerializer.Serialize(lines, LineJson);
        return json.Length <= MaxSnapshotLength
            ? Result<string?>.From(json)
            : ServiceError.Invalid("Items", "Danh sách món quá dài cho một mã QR; hãy bớt món hoặc thanh toán đồ thêm sau.");
    }

    /// <summary>Reads a snapshot: null/empty JSON → no lines (true); malformed JSON or a bad line → false.</summary>
    internal static bool TryReadSnapshot(string? json, out List<AddOnLine> lines)
    {
        lines = [];
        if (string.IsNullOrWhiteSpace(json))
        {
            return true;
        }

        try
        {
            lines = JsonSerializer.Deserialize<List<AddOnLine>>(json, LineJson) ?? [];
        }
        catch (JsonException)
        {
            return false;
        }

        return lines.All(l => l.Quantity is >= 1 and <= 99 && l.UnitPrice >= 0 && !string.IsNullOrWhiteSpace(l.Name));
    }

    private static ProductView ToView(Product p) => new(p.Id, p.BranchId, p.Name, p.Category, p.Price, p.IsActive, p.SortOrder);

    internal static BillItemView ToView(BillItem i) => new(i.Id, i.Name, i.UnitPrice, i.Quantity, i.Amount, i.PaymentMethod, i.CreatedAt);
}
