using ChonRieng.Api.Features.Catalog;
using ChonRieng.Api.Features.Common;
using ChonRieng.Domain.Entities;
using ChonRieng.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChonRieng.Api.Features.Sales;

/// <summary>POS session lifecycle (TASK-007/008): payment is taken at check-in (decision D5).</summary>
public sealed class SessionService(AppDbContext db, TimeProvider clock)
{
    public async Task<Result<SessionView>> CheckInAsync(CurrentUser user, CheckInSessionRequest request, CancellationToken ct)
    {
        var seat = await db.Seats.Include(s => s.Zone).SingleOrDefaultAsync(s => s.Id == request.SeatId, ct);
        if (seat is null || !user.CanAccessBranch(seat.Zone.BranchId))
        {
            return ServiceError.NotFound("Không tìm thấy chỗ ngồi.");
        }

        var branchId = seat.Zone.BranchId;
        if (seat.Status == SeatStatus.Maintenance)
        {
            return ServiceError.Conflict("Chỗ đang bảo trì", "Hãy chọn chỗ khác.");
        }

        if (await db.Sessions.AnyAsync(s => s.SeatId == seat.Id && s.Status == SessionStatus.Active, ct))
        {
            return SeatTaken();
        }

        var now = clock.GetUtcNow();
        var plans = await db.PricePlans.Where(p => p.BranchId == null || p.BranchId == branchId).ToListAsync(ct);
        var plan = CatalogService.ResolveActivePlans(plans, branchId, now).SingleOrDefault(p => p.Id == request.PricePlanId);
        if (plan is null)
        {
            return ServiceError.Invalid("PricePlanId", "Bảng giá không áp dụng cho chi nhánh này hoặc đã hết hiệu lực.");
        }

        var shift = await ResolveShiftAsync(user, branchId, now, ct);
        if (shift.Error is not null)
        {
            return shift.Error;
        }

        Customer? customer = null;
        if (request.CustomerId is { } customerId)
        {
            customer = await db.Customers.Include(c => c.User).SingleOrDefaultAsync(c => c.Id == customerId, ct);
            if (customer is null)
            {
                return ServiceError.Invalid("CustomerId", "Không tìm thấy khách hàng.");
            }
        }

        var session = new Session
        {
            SeatId = seat.Id,
            CustomerId = customer?.Id,
            PricePlanId = plan.Id,
            StartAt = now,
            EndAt = now.AddMinutes(plan.DurationMinutes),
            Status = SessionStatus.Active,
        };
        var bill = new Bill
        {
            Code = await NewUniqueBillCodeAsync(ct),
            BranchId = branchId,
            ShiftId = shift.Value,
            SessionId = session.Id,
            Total = plan.Price,
            PaymentMethod = request.PaymentMethod,
            CreatedAt = now,
        };
        db.Sessions.Add(session);
        db.Bills.Add(bill);

        var points = 0;
        if (customer is not null)
        {
            // A member identified at the counter gets points automatically; the bill can't be claimed again.
            bill.ClaimedByCustomerId = customer.Id;
            bill.ClaimedAt = now;
            var tx = Loyalty.Award(customer, bill, bill.Total, now, $"Tích điểm hóa đơn {bill.Code}");
            db.PointTransactions.Add(tx);
            points = tx.Points;
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // The filtered unique index lost a race with another tablet.
            return SeatTaken();
        }

        return (await GetAsync(session.Id, points, ct))!;
    }

    public async Task<Result<SessionView>> ExtendAsync(CurrentUser user, Guid sessionId, CancellationToken ct)
    {
        var session = await db.Sessions.Include(s => s.Seat).ThenInclude(s => s.Zone).Include(s => s.PricePlan)
            .Include(s => s.Bill).SingleOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session is null || !user.CanAccessBranch(session.Seat.Zone.BranchId))
        {
            return ServiceError.NotFound();
        }

        if (session.Status != SessionStatus.Active || session.Bill is null)
        {
            return ServiceError.Conflict("Lượt ngồi đã kết thúc", "Chỉ gia hạn được lượt đang diễn ra.");
        }

        var now = clock.GetUtcNow();
        var extension = new SessionExtension
        {
            SessionId = session.Id,
            AddedMinutes = session.PricePlan.DurationMinutes,
            Price = session.PricePlan.ExtendPrice,
            CreatedAt = now,
        };
        db.SessionExtensions.Add(extension);
        // Overdue time is not free: the extension counts from the scheduled end.
        session.EndAt = session.EndAt.AddMinutes(extension.AddedMinutes);
        // Every extension is added to the same bill, so one code earns points for the whole visit.
        session.Bill.Total += extension.Price;

        var points = 0;
        if (session.Bill.ClaimedByCustomerId is { } customerId)
        {
            var customer = await db.Customers.SingleAsync(c => c.Id == customerId, ct);
            var tx = Loyalty.Award(customer, session.Bill, extension.Price, now, $"Gia hạn hóa đơn {session.Bill.Code}");
            db.PointTransactions.Add(tx);
            points = tx.Points;
        }

        await db.SaveChangesAsync(ct);
        return (await GetAsync(session.Id, points, ct))!;
    }

    public async Task<Result<SessionView>> CloseAsync(CurrentUser user, Guid sessionId, CancellationToken ct)
    {
        var session = await db.Sessions.Include(s => s.Seat).ThenInclude(s => s.Zone).SingleOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session is null || !user.CanAccessBranch(session.Seat.Zone.BranchId))
        {
            return ServiceError.NotFound();
        }

        if (session.Status != SessionStatus.Active)
        {
            return ServiceError.Conflict("Lượt ngồi đã kết thúc", "Chỗ này đã được trả.");
        }

        var now = clock.GetUtcNow();
        // Expired = the customer stayed past the paid time (decision D7); Closed = left in time.
        session.Status = now > session.EndAt ? SessionStatus.Expired : SessionStatus.Closed;
        session.ClosedAt = now;
        await db.SaveChangesAsync(ct);
        return (await GetAsync(session.Id, 0, ct))!;
    }

    public async Task<Result<IReadOnlyList<SessionView>>> ActiveAsync(CurrentUser user, Guid branchId, CancellationToken ct)
    {
        if (!user.CanAccessBranch(branchId))
        {
            return ServiceError.NotFound();
        }

        return Result<IReadOnlyList<SessionView>>.From(await Query(db.Sessions.Where(s => s.Status == SessionStatus.Active && s.Seat.Zone.BranchId == branchId), 0, ct));
    }

    public async Task<Result<SessionView>> GetByBillCodeAsync(CurrentUser user, string code, CancellationToken ct)
    {
        var normalized = Loyalty.NormalizeCode(code);
        var view = (await Query(db.Sessions.Where(s => s.Bill != null && s.Bill.Code == normalized), 0, ct)).SingleOrDefault();
        return view is null || !user.CanAccessBranch(view.BranchId) ? ServiceError.NotFound("Không tìm thấy hóa đơn.") : view;
    }

    public async Task<IReadOnlyList<CustomerLookupResult>> LookupCustomersAsync(string q, CancellationToken ct)
    {
        var term = q.Trim();
        if (term.Length < 3)
        {
            return [];
        }

        var upper = term.ToUpperInvariant();
        return await db.Customers.AsNoTracking()
            .Where(c => c.MemberCode == upper || c.User.PhoneNumber == term || c.User.NormalizedEmail == upper)
            .Take(5)
            .Select(c => new CustomerLookupResult(c.Id, c.User.FullName, c.Tier, c.Points, c.MemberCode))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Every bill belongs to a shift (Bill.ShiftId is required). Staff sell in the shift they checked in to;
    /// Admin/Manager not on the rota use the branch's shift running right now.
    /// </summary>
    private async Task<Result<Guid>> ResolveShiftAsync(CurrentUser user, Guid branchId, DateTimeOffset now, CancellationToken ct)
    {
        var ownShift = await db.Attendances
            .Where(a => a.Employee.UserId == user.UserId && a.CheckInAt != null && a.CheckOutAt == null && a.Shift.BranchId == branchId)
            .Select(a => (Guid?)a.ShiftId)
            .FirstOrDefaultAsync(ct);
        if (ownShift is { } id)
        {
            return id;
        }

        if (user.IsAdmin || user.IsManager)
        {
            var running = await db.Shifts
                .Where(s => s.BranchId == branchId && s.Status == ShiftStatus.Published && s.StartAt <= now && now < s.EndAt)
                .OrderBy(s => s.StartAt)
                .Select(s => (Guid?)s.Id)
                .FirstOrDefaultAsync(ct);
            if (running is { } runningId)
            {
                return runningId;
            }

            return ServiceError.Conflict("Chưa có ca đang mở", "Chi nhánh chưa có ca nào đang diễn ra để ghi nhận hóa đơn.");
        }

        return ServiceError.Conflict("Bạn chưa tham gia ca", "Hãy bấm 'Tham gia ca' tại chi nhánh này trước khi bán.");
    }

    private async Task<string> NewUniqueBillCodeAsync(CancellationToken ct)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var code = Loyalty.NewBillCode();
            if (!await db.Bills.AnyAsync(b => b.Code == code, ct))
            {
                return code;
            }
        }

        throw new InvalidOperationException("Could not generate a unique bill code.");
    }

    private async Task<SessionView?> GetAsync(Guid sessionId, int pointsAwarded, CancellationToken ct) =>
        (await Query(db.Sessions.Where(s => s.Id == sessionId), pointsAwarded, ct)).SingleOrDefault();

    private async Task<IReadOnlyList<SessionView>> Query(IQueryable<Session> query, int pointsAwarded, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var rows = await query.AsNoTracking()
            .OrderBy(s => s.EndAt)
            .Select(s => new
            {
                s.Id,
                s.Seat.Zone.BranchId,
                s.SeatId,
                SeatCode = s.Seat.Code,
                s.PricePlanId,
                PlanName = s.PricePlan.Name,
                s.StartAt,
                s.EndAt,
                s.Status,
                Extensions = s.Extensions.Count(),
                Customer = s.Customer != null ? new SessionCustomer(s.Customer.Id, s.Customer.User.FullName, s.Customer.Tier) : null,
                Bill = new SessionBill(s.Bill!.Id, s.Bill.Code, s.Bill.Total, s.Bill.PaymentMethod, s.Bill.ClaimedByCustomerId != null, s.Bill.CreatedAt),
            })
            .ToListAsync(ct);

        return rows.Select(r => new SessionView(r.Id, r.BranchId, r.SeatId, r.SeatCode, r.PricePlanId, r.PlanName, r.StartAt, r.EndAt,
            r.Status, (int)Math.Floor((r.EndAt - now).TotalMinutes), r.Extensions, r.Customer, r.Bill, pointsAwarded)).ToList();
    }

    private static ServiceError SeatTaken() => ServiceError.Conflict("Chỗ đã có người", "Chỗ này không còn trống; hãy chọn chỗ khác.");
}
