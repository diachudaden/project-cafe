using ChonRieng.Domain.Payments;

namespace ChonRieng.Application.Payments;

public sealed record SeatAllocation(IHoldableSeat? Seat, bool IsDefault, bool NoSeatAvailable)
{
    public static readonly SeatAllocation NotRequired = new(null, false, false);
    public static readonly SeatAllocation NoneLeft = new(null, false, true);
}

/// <summary>Mặc định: ghế trống có SortOrder nhỏ nhất (quản lý xếp thứ tự ưu tiên khi setup chi nhánh).</summary>
public sealed class LowestSortOrderSeatStrategy : IDefaultSeatStrategy
{
    public IHoldableSeat? Pick(IReadOnlyList<IHoldableSeat> candidates, IPayableBill bill) =>
        candidates.OrderBy(s => s.SortOrder).ThenBy(s => s.Id).FirstOrDefault();
}

/// <summary>
/// Xếp chỗ cho bill vừa được trả tiền, theo thứ tự ưu tiên trong SPEC mục 7.
/// Chỉ thay đổi entity trong store, không SaveChanges — người gọi lưu cùng transaction với bill và ledger.
/// </summary>
public sealed class SeatAllocator(IPaymentStore store, IDefaultSeatStrategy strategy)
{
    public async Task<SeatAllocation> AllocateAsync(IPayableBill bill, DateTime nowUtc, CancellationToken ct)
    {
        if (!bill.RequiresSeat) return SeatAllocation.NotRequired;

        // 1. Ghế còn đang giữ cho đúng bill này (kể cả quá hạn mà job chưa kịp nhả).
        var held = await store.FindSeatHeldForBillAsync(bill.Id, ct);
        if (held is not null && held.IsHeldFor(bill.Id))
            return await TakeAsync(bill, held, isDefault: false, nowUtc, ct);

        // 2. Ghế khách chọn đã bị nhả nhưng vẫn còn trống.
        string? seatType = bill.RequestedSeatType;
        if (bill.RequestedSeatId is int requestedId)
        {
            var requested = await store.GetSeatAsync(requestedId, ct);
            if (requested is not null)
            {
                if (requested.IsAvailable(nowUtc))
                    return await TakeAsync(bill, requested, isDefault: false, nowUtc, ct);

                seatType ??= requested.SeatType;
            }
        }

        // 3. Ghế mặc định cùng chi nhánh, cùng loại.
        var candidates = await store.GetAvailableSeatsAsync(bill.BranchId, seatType, nowUtc, ct);
        var picked = strategy.Pick(candidates, bill);
        if (picked is not null)
            return await TakeAsync(bill, picked, isDefault: true, nowUtc, ct);

        // 4. Hết chỗ.
        bill.FlagNeedsAttention("NO_SEAT");
        return SeatAllocation.NoneLeft;
    }

    private async Task<SeatAllocation> TakeAsync(IPayableBill bill, IHoldableSeat seat, bool isDefault, DateTime nowUtc, CancellationToken ct)
    {
        seat.ReleaseHold();
        await store.StartSessionAsync(bill, seat, nowUtc, ct);
        return new SeatAllocation(seat, isDefault, NoSeatAvailable: false);
    }
}
