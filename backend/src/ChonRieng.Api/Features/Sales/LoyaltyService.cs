using ChonRieng.Api.Features.Common;
using ChonRieng.Domain.Entities;
using ChonRieng.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChonRieng.Api.Features.Sales;

public sealed class LoyaltyService(AppDbContext db, TimeProvider clock)
{
    public async Task<Result<ClaimBillResponse>> ClaimAsync(CurrentUser user, string code, CancellationToken ct)
    {
        var customer = await OwnCustomerAsync(user, ct);
        if (customer is null)
        {
            return ServiceError.NotFound("Tài khoản không có hồ sơ khách hàng.");
        }

        var normalized = Loyalty.NormalizeCode(code);
        var bill = await db.Bills.SingleOrDefaultAsync(b => b.Code == normalized, ct);
        if (bill is null)
        {
            return ServiceError.NotFound("Mã bill không đúng. Hãy kiểm tra lại 8 ký tự trên hóa đơn.");
        }

        if (bill.ClaimedByCustomerId is not null)
        {
            return ServiceError.Conflict("Bill đã được tích điểm", "Mỗi bill chỉ tích điểm một lần.");
        }

        var now = clock.GetUtcNow();
        if (now - bill.CreatedAt > TimeSpan.FromDays(Loyalty.ClaimWindowDays))
        {
            return ServiceError.Conflict("Bill đã quá hạn", $"Chỉ tích điểm trong {Loyalty.ClaimWindowDays} ngày kể từ lúc thanh toán.");
        }

        bill.ClaimedByCustomerId = customer.Id;
        bill.ClaimedAt = now;
        var tx = Loyalty.Award(customer, bill, bill.Total, now, $"Tích điểm hóa đơn {bill.Code}");
        db.PointTransactions.Add(tx);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // ClaimedByCustomerId is a concurrency token: someone claimed the same bill a moment earlier.
            return ServiceError.Conflict("Bill đã được tích điểm", "Mỗi bill chỉ tích điểm một lần.");
        }

        return new ClaimBillResponse(bill.Code, bill.Total, tx.Points, customer.Points, customer.Tier);
    }

    public async Task<Result<MyPointsResponse>> PointsAsync(CurrentUser user, CancellationToken ct)
    {
        var customer = await OwnCustomerAsync(user, ct);
        if (customer is null)
        {
            return ServiceError.NotFound("Tài khoản không có hồ sơ khách hàng.");
        }

        if (string.IsNullOrEmpty(customer.MemberCode) || string.IsNullOrEmpty(customer.Tier) || customer.Tier == "DEMO")
        {
            // Profiles created before member codes existed get one on first use.
            customer.MemberCode ??= Loyalty.NewMemberCode();
            customer.Tier = Loyalty.TierFor(customer.TotalSpent);
            await db.SaveChangesAsync(ct);
        }

        var history = await db.PointTransactions.AsNoTracking()
            .Where(p => p.CustomerId == customer.Id)
            .OrderByDescending(p => p.CreatedAt)
            .Take(100)
            .Select(p => new PointHistoryItem(p.Id, p.Points, p.Reason, p.Bill != null ? p.Bill.Code : null, p.CreatedAt))
            .ToListAsync(ct);

        var (next, threshold) = customer.Tier switch
        {
            Loyalty.Bronze => (Loyalty.Silver, Loyalty.SilverThreshold),
            Loyalty.Silver => (Loyalty.Gold, Loyalty.GoldThreshold),
            _ => ((string?)null, 0m),
        };
        return new MyPointsResponse(customer.Points, customer.Tier, customer.TotalSpent, next,
            next is null ? 0 : Math.Max(0, threshold - customer.TotalSpent), customer.MemberCode!, history);
    }

    public async Task<IReadOnlyList<MyBill>> BillsAsync(CurrentUser user, CancellationToken ct) =>
        await db.Bills.AsNoTracking()
            .Where(b => b.ClaimedByCustomer != null && b.ClaimedByCustomer.UserId == user.UserId)
            .OrderByDescending(b => b.CreatedAt)
            .Take(100)
            .Select(b => new MyBill(b.Code, b.Branch.Name, b.Total, b.CreatedAt, b.ClaimedAt, b.PointTransactions.Sum(p => p.Points)))
            .ToListAsync(ct);

    public async Task<MyActiveSession?> CurrentSessionAsync(CurrentUser user, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var row = await db.Sessions.AsNoTracking()
            .Where(s => s.Status == SessionStatus.Active && s.Customer != null && s.Customer.UserId == user.UserId)
            .OrderByDescending(s => s.StartAt)
            .Select(s => new { s.Id, Branch = s.Seat.Zone.Branch.Name, s.Seat.Code, s.StartAt, s.EndAt })
            .FirstOrDefaultAsync(ct);
        return row is null ? null : new MyActiveSession(row.Id, row.Branch, row.Code, row.StartAt, row.EndAt, (int)Math.Floor((row.EndAt - now).TotalMinutes));
    }

    private Task<Customer?> OwnCustomerAsync(CurrentUser user, CancellationToken ct) =>
        db.Customers.SingleOrDefaultAsync(c => c.UserId == user.UserId, ct);
}
