using ChonRieng.Api.Features.Common;
using ChonRieng.Domain.Entities;
using ChonRieng.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChonRieng.Api.Features.Catalog;

public sealed class CatalogService(AppDbContext db, TimeProvider clock)
{
    public const int EndingSoonMinutes = 15;

    public async Task<IReadOnlyList<PublicBranchResponse>> ListBranchesAsync(double? lat, double? lng, CancellationToken ct)
    {
        var branches = await db.Branches.AsNoTracking().Where(b => b.IsActive).ToListAsync(ct);
        var availability = await GetAvailabilityByBranchAsync(branches.Select(b => b.Id).ToArray(), ct);
        var localTime = TimeOnly.FromDateTime(clock.Now().DateTime);

        var result = branches.Select(b => new PublicBranchResponse(
            b.Id, b.Name, b.Address, b.Latitude, b.Longitude, b.Is24h, b.OpenTime, b.CloseTime, b.Phone,
            VietnamTime.IsOpen(b.Is24h, b.OpenTime, b.CloseTime, localTime),
            lat is not null && lng is not null && b.Latitude is not null && b.Longitude is not null
                ? Math.Round(DistanceKm(lat.Value, lng.Value, (double)b.Latitude, (double)b.Longitude), 1)
                : null,
            availability.GetValueOrDefault(b.Id, [])));

        return (lat is not null && lng is not null
            ? result.OrderBy(b => b.DistanceKm ?? double.MaxValue)
            : result.OrderBy(b => b.Name)).ToList();
    }

    public async Task<Result<IReadOnlyList<SeatTypeAvailability>>> GetAvailabilityAsync(Guid branchId, CancellationToken ct)
    {
        if (!await db.Branches.AnyAsync(b => b.Id == branchId && b.IsActive, ct))
        {
            return ServiceError.NotFound("Không tìm thấy chi nhánh đang hoạt động.");
        }

        var availability = await GetAvailabilityByBranchAsync([branchId], ct);
        return Result<IReadOnlyList<SeatTypeAvailability>>.From(availability.GetValueOrDefault(branchId, []));
    }

    public async Task<IReadOnlyList<PricePlanView>> GetActivePlansAsync(Guid branchId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var plans = await db.PricePlans.AsNoTracking()
            .Where(p => p.BranchId == null || p.BranchId == branchId)
            .ToListAsync(ct);
        return ResolveActivePlans(plans, branchId, now).Select(p => ToView(p, now)).ToList();
    }

    /// <summary>
    /// Plans valid now for the branch. A branch-specific plan overrides a chain-wide plan
    /// with the same duration (decision D4).
    /// </summary>
    public static IReadOnlyList<PricePlan> ResolveActivePlans(IEnumerable<PricePlan> plans, Guid branchId, DateTimeOffset now)
    {
        return plans
            .Where(p => (p.BranchId is null || p.BranchId == branchId) && IsValidAt(p, now))
            .GroupBy(p => p.DurationMinutes)
            .Select(g => g.OrderByDescending(p => p.BranchId is not null).ThenBy(p => p.Price).First())
            .OrderBy(p => p.DurationMinutes)
            .ToList();
    }

    public static bool IsValidAt(PricePlan plan, DateTimeOffset now) =>
        (plan.ValidFrom is null || plan.ValidFrom <= now) && (plan.ValidTo is null || now < plan.ValidTo);

    public async Task<Result<FloorResponse>> GetFloorAsync(CurrentUser user, Guid branchId, CancellationToken ct)
    {
        if (!user.CanAccessBranch(branchId))
        {
            return ServiceError.NotFound();
        }

        var branch = await db.Branches.AsNoTracking().SingleOrDefaultAsync(b => b.Id == branchId, ct);
        if (branch is null)
        {
            return ServiceError.NotFound();
        }

        var zones = await db.Zones.AsNoTracking()
            .Where(z => z.BranchId == branchId)
            .OrderBy(z => z.Name)
            .Select(z => new { z.Id, z.Name, Seats = z.Seats.OrderBy(s => s.Code).ToList() })
            .ToListAsync(ct);
        var active = await db.Sessions.AsNoTracking()
            .Where(s => s.Status == SessionStatus.Active && s.Seat.Zone.BranchId == branchId)
            .Select(s => new
            {
                s.Id,
                s.SeatId,
                s.StartAt,
                s.EndAt,
                CustomerName = s.Customer != null ? s.Customer.User.FullName : null,
                BillCode = s.Bill != null ? s.Bill.Code : "",
                BillTotal = s.Bill != null ? s.Bill.Total : 0,
            })
            .ToDictionaryAsync(s => s.SeatId, ct);

        var now = clock.GetUtcNow();
        var heldSeatIds = await db.PaymentRequests.AsNoTracking()
            .Where(p => p.BranchId == branchId
                && p.Status == PaymentRequestStatus.Pending
                && p.ExpiresAt > now)
            .Select(p => p.SeatId)
            .ToHashSetAsync(ct);

        return new FloorResponse(branch.Id, branch.Name, zones.Select(z => new FloorZone(z.Id, z.Name, z.Seats.Select(seat =>
        {
            if (active.TryGetValue(seat.Id, out var s))
            {
                var minutesLeft = (int)Math.Floor((s.EndAt - now).TotalMinutes);
                var state = minutesLeft < 0 ? SeatLiveState.Overdue
                    : minutesLeft <= EndingSoonMinutes ? SeatLiveState.EndingSoon
                    : SeatLiveState.Occupied;
                return new FloorSeat(seat.Id, seat.Code, seat.Type, state,
                    new SeatSessionInfo(s.Id, s.StartAt, s.EndAt, minutesLeft, s.CustomerName, s.BillCode, s.BillTotal));
            }

            if (heldSeatIds.Contains(seat.Id))
            {
                return new FloorSeat(seat.Id, seat.Code, seat.Type, SeatLiveState.Held, null);
            }

            return new FloorSeat(seat.Id, seat.Code, seat.Type,
                seat.Status == SeatStatus.Maintenance ? SeatLiveState.Maintenance : SeatLiveState.Available, null);
        }).ToList())).ToList());
    }

    public async Task<Result<ZoneResponse>> CreateZoneAsync(CurrentUser user, Guid branchId, ZoneRequest request, CancellationToken ct)
    {
        if (!user.CanManageBranch(branchId) || !await db.Branches.AnyAsync(b => b.Id == branchId, ct))
        {
            return ServiceError.NotFound();
        }

        if (await db.Zones.AnyAsync(z => z.BranchId == branchId && z.Name == request.Name.Trim(), ct))
        {
            return ServiceError.Conflict("Trùng tên khu vực", "Chi nhánh đã có khu vực cùng tên.");
        }

        var zone = new Zone { BranchId = branchId, Name = request.Name.Trim() };
        db.Zones.Add(zone);
        await db.SaveChangesAsync(ct);
        return new ZoneResponse(zone.Id, zone.BranchId, zone.Name);
    }

    public async Task<Result<ZoneResponse>> UpdateZoneAsync(CurrentUser user, Guid zoneId, ZoneRequest request, CancellationToken ct)
    {
        var zone = await db.Zones.SingleOrDefaultAsync(z => z.Id == zoneId, ct);
        if (zone is null || !user.CanManageBranch(zone.BranchId))
        {
            return ServiceError.NotFound();
        }

        zone.Name = request.Name.Trim();
        await db.SaveChangesAsync(ct);
        return new ZoneResponse(zone.Id, zone.BranchId, zone.Name);
    }

    public async Task<Result<Done>> DeleteZoneAsync(CurrentUser user, Guid zoneId, CancellationToken ct)
    {
        var zone = await db.Zones.SingleOrDefaultAsync(z => z.Id == zoneId, ct);
        if (zone is null || !user.CanManageBranch(zone.BranchId))
        {
            return ServiceError.NotFound();
        }

        if (await db.Seats.AnyAsync(s => s.ZoneId == zoneId, ct))
        {
            return ServiceError.Conflict("Khu vực còn chỗ ngồi", "Hãy xóa hoặc chuyển các chỗ ngồi trước khi xóa khu vực.");
        }

        db.Zones.Remove(zone);
        await db.SaveChangesAsync(ct);
        return new Done();
    }

    public async Task<Result<SeatResponse>> CreateSeatAsync(CurrentUser user, Guid zoneId, SeatRequest request, CancellationToken ct)
    {
        var zone = await db.Zones.AsNoTracking().SingleOrDefaultAsync(z => z.Id == zoneId, ct);
        if (zone is null || !user.CanManageBranch(zone.BranchId))
        {
            return ServiceError.NotFound();
        }

        var code = request.Code.Trim().ToUpperInvariant();
        if (await db.Seats.AnyAsync(s => s.ZoneId == zoneId && s.Code == code, ct))
        {
            return ServiceError.Conflict("Trùng mã chỗ", $"Khu vực đã có chỗ {code}.");
        }

        var seat = new Seat { ZoneId = zoneId, Code = code, Type = request.Type, Status = SeatStatus.Available };
        db.Seats.Add(seat);
        await db.SaveChangesAsync(ct);
        return ToResponse(seat);
    }

    public async Task<Result<SeatResponse>> UpdateSeatAsync(CurrentUser user, Guid seatId, SeatRequest request, CancellationToken ct)
    {
        var seat = await db.Seats.Include(s => s.Zone).SingleOrDefaultAsync(s => s.Id == seatId, ct);
        if (seat is null || !user.CanManageBranch(seat.Zone.BranchId))
        {
            return ServiceError.NotFound();
        }

        if (request.ZoneId is { } targetZoneId && targetZoneId != seat.ZoneId)
        {
            var target = await db.Zones.AsNoTracking().SingleOrDefaultAsync(z => z.Id == targetZoneId, ct);
            if (target is null || target.BranchId != seat.Zone.BranchId)
            {
                return ServiceError.Invalid("ZoneId", "Chỉ được chuyển chỗ sang khu vực cùng chi nhánh.");
            }

            seat.ZoneId = targetZoneId;
        }

        seat.Code = request.Code.Trim().ToUpperInvariant();
        seat.Type = request.Type;
        await db.SaveChangesAsync(ct);
        return ToResponse(seat);
    }

    public async Task<Result<Done>> DeleteSeatAsync(CurrentUser user, Guid seatId, CancellationToken ct)
    {
        var seat = await db.Seats.Include(s => s.Zone).SingleOrDefaultAsync(s => s.Id == seatId, ct);
        if (seat is null || !user.CanManageBranch(seat.Zone.BranchId))
        {
            return ServiceError.NotFound();
        }

        if (await db.Sessions.AnyAsync(s => s.SeatId == seatId, ct))
        {
            return ServiceError.Conflict("Chỗ đã có lịch sử sử dụng", "Không thể xóa chỗ đã có lượt ngồi; hãy chuyển sang bảo trì.");
        }

        db.Seats.Remove(seat);
        await db.SaveChangesAsync(ct);
        return new Done();
    }

    public async Task<Result<SeatResponse>> SetMaintenanceAsync(CurrentUser user, Guid seatId, bool maintenance, CancellationToken ct)
    {
        var seat = await db.Seats.Include(s => s.Zone).SingleOrDefaultAsync(s => s.Id == seatId, ct);
        if (seat is null || !user.CanManageBranch(seat.Zone.BranchId))
        {
            return ServiceError.NotFound();
        }

        if (maintenance && await db.Sessions.AnyAsync(s => s.SeatId == seatId && s.Status == SessionStatus.Active, ct))
        {
            return ServiceError.Conflict("Chỗ đang có khách", "Hãy trả chỗ trước khi chuyển sang bảo trì.");
        }

        // Occupancy is derived from active sessions, so the stored status is only Available or Maintenance.
        seat.Status = maintenance ? SeatStatus.Maintenance : SeatStatus.Available;
        await db.SaveChangesAsync(ct);
        return ToResponse(seat);
    }

    public async Task<Result<IReadOnlyList<PricePlanView>>> ListManagedPlansAsync(CurrentUser user, Guid branchId, CancellationToken ct)
    {
        if (!user.CanAccessBranch(branchId))
        {
            return ServiceError.NotFound();
        }

        var now = clock.GetUtcNow();
        var plans = await db.PricePlans.AsNoTracking()
            .Where(p => p.BranchId == null || p.BranchId == branchId)
            .OrderBy(p => p.BranchId == null).ThenBy(p => p.DurationMinutes).ThenBy(p => p.Name)
            .ToListAsync(ct);
        return Result<IReadOnlyList<PricePlanView>>.From(plans.Select(p => ToView(p, now)).ToList());
    }

    public async Task<Result<PricePlanView>> CreatePlanAsync(CurrentUser user, PricePlanRequest request, CancellationToken ct)
    {
        var error = await ValidatePlanAsync(user, request, ct);
        if (error is not null)
        {
            return error;
        }

        var plan = new PricePlan();
        Apply(plan, request);
        db.PricePlans.Add(plan);
        await db.SaveChangesAsync(ct);
        return ToView(plan, clock.GetUtcNow());
    }

    public async Task<Result<PricePlanView>> UpdatePlanAsync(CurrentUser user, Guid planId, PricePlanRequest request, CancellationToken ct)
    {
        var plan = await db.PricePlans.SingleOrDefaultAsync(p => p.Id == planId, ct);
        if (plan is null || !CanManagePlan(user, plan.BranchId))
        {
            return ServiceError.NotFound();
        }

        var error = await ValidatePlanAsync(user, request, ct);
        if (error is not null)
        {
            return error;
        }

        Apply(plan, request);
        await db.SaveChangesAsync(ct);
        return ToView(plan, clock.GetUtcNow());
    }

    private async Task<ServiceError?> ValidatePlanAsync(CurrentUser user, PricePlanRequest request, CancellationToken ct)
    {
        if (!CanManagePlan(user, request.BranchId))
        {
            return request.BranchId is null
                ? ServiceError.Forbidden("Chỉ Admin được tạo hoặc sửa bảng giá toàn chuỗi.")
                : ServiceError.NotFound();
        }

        if (request.BranchId is { } branchId && !await db.Branches.AnyAsync(b => b.Id == branchId, ct))
        {
            return ServiceError.Invalid("BranchId", "Chi nhánh không tồn tại.");
        }

        if (request.ValidFrom is not null && request.ValidTo is not null && request.ValidTo <= request.ValidFrom)
        {
            return ServiceError.Invalid("ValidTo", "Ngày kết thúc phải sau ngày bắt đầu.");
        }

        return null;
    }

    private static bool CanManagePlan(CurrentUser user, Guid? branchId) =>
        branchId is { } id ? user.CanManageBranch(id) : user.IsAdmin;

    private static void Apply(PricePlan plan, PricePlanRequest r)
    {
        plan.BranchId = r.BranchId;
        plan.Name = r.Name.Trim();
        plan.DurationMinutes = r.DurationMinutes;
        plan.Price = r.Price;
        plan.ExtendPrice = r.ExtendPrice;
        plan.ValidFrom = r.ValidFrom;
        plan.ValidTo = r.ValidTo;
    }

    private async Task<Dictionary<Guid, IReadOnlyList<SeatTypeAvailability>>> GetAvailabilityByBranchAsync(
        IReadOnlyCollection<Guid> branchIds,
        CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var seats = await db.Seats.AsNoTracking()
            .Where(s => branchIds.Contains(s.Zone.BranchId))
            .Select(s => new
            {
                s.Zone.BranchId,
                s.Type,
                s.Status,
                Busy = s.Sessions.Any(x => x.Status == SessionStatus.Active)
                    || db.PaymentRequests.Any(p => p.SeatId == s.Id
                        && p.Status == PaymentRequestStatus.Pending
                        && p.ExpiresAt > now),
            })
            .ToListAsync(ct);

        return seats.GroupBy(s => s.BranchId).ToDictionary(
            g => g.Key,
            g => (IReadOnlyList<SeatTypeAvailability>)g.GroupBy(s => s.Type).OrderBy(t => t.Key).Select(t => new SeatTypeAvailability(
                t.Key,
                t.Count(),
                t.Count(s => s.Status != SeatStatus.Maintenance && !s.Busy),
                t.Count(s => s.Status == SeatStatus.Maintenance))).ToList());
    }

    private static PricePlanView ToView(PricePlan p, DateTimeOffset now) =>
        new(p.Id, p.BranchId, p.Name, p.DurationMinutes, p.Price, p.ExtendPrice, p.ValidFrom, p.ValidTo, IsValidAt(p, now));

    private static SeatResponse ToResponse(Seat s) => new(s.Id, s.ZoneId, s.Code, s.Type, s.Status);

    private static double DistanceKm(double lat1, double lng1, double lat2, double lng2)
    {
        const double earthRadiusKm = 6371;
        double Rad(double degrees) => degrees * Math.PI / 180;
        var dLat = Rad(lat2 - lat1);
        var dLng = Rad(lng2 - lng1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return earthRadiusKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}
