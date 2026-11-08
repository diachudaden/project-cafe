using ChonRieng.Api.Features.Catalog;
using ChonRieng.Api.Features.Common;
using ChonRieng.Api.Features.Sales;
using ChonRieng.Api.Services;
using ChonRieng.Application.Persistence;
using ChonRieng.Domain.Entities;
using ChonRieng.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace ChonRieng.UnitTests.Features;

public sealed class PaymentRequestTests : IDisposable
{
    private readonly AppDbContext _db = new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase($"PaymentRequestTests-{Guid.NewGuid()}").Options);
    private readonly TestClock _clock = new(new DateTimeOffset(2026, 11, 2, 10, 0, 0, VietnamTime.Offset));
    private readonly Branch _branch = new() { Name = "Branch", Address = "x", Is24h = true, OpenTime = new TimeOnly(0, 0), CloseTime = new TimeOnly(0, 0) };
    private readonly Guid _managerId = Guid.CreateVersion7();
    private readonly Seat _seat;
    private readonly Seat _seat2;
    private readonly PricePlan _plan = new() { Name = "4 tiếng", DurationMinutes = 240, Price = 45_000, ExtendPrice = 40_000 };
    private readonly Shift _shift;

    public PaymentRequestTests()
    {
        var zone = new Zone { BranchId = _branch.Id, Name = "Khu A" };
        _seat = new Seat { ZoneId = zone.Id, Code = "A-01", Type = SeatType.Single };
        _seat2 = new Seat { ZoneId = zone.Id, Code = "A-02", Type = SeatType.Single };
        _shift = new Shift
        {
            BranchId = _branch.Id,
            Date = DateOnly.FromDateTime(_clock.Now().DateTime),
            Type = ShiftType.Morning,
            StartAt = _clock.GetUtcNow().AddHours(-1),
            EndAt = _clock.GetUtcNow().AddHours(7),
            RequiredStaff = 1,
            Status = ShiftStatus.Published,
        };
        _db.AddRange(_branch, zone, _seat, _seat2, _plan, _shift);
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    private CurrentUser Manager => new(
        _managerId,
        new HashSet<string> { BusinessRoles.BranchManager },
        BranchAccessScope.ForBranches(new HashSet<Guid> { _branch.Id }));

    private PaymentRequestService Payments => new(
        _db,
        Options.Create(new PaymentOptions
        {
            Bank = "Vietcom&bank",
            AccountNumber = "123456789",
            AccountHolder = "CAFE CR",
            CodePrefix = "TT",
            QrImageBaseUrl = "https://qr.example.invalid/image",
            HoldMinutes = 10,
            WebhookSecret = "test-secret",
            TimestampToleranceSeconds = 300,
        }),
        _clock,
        new SessionService(_db, _clock));

    [Fact]
    public async Task Check_in_request_uses_server_price_and_holds_the_seat()
    {
        var result = await Payments.CreateCheckInAsync(
            Manager,
            new CreateCheckInPaymentRequest(_seat.Id, _plan.Id, null),
            default);

        var payment = result.Value!;
        Assert.True(result.IsSuccess);
        Assert.Equal(45_000, payment.Amount);
        Assert.StartsWith("TT", payment.Code, StringComparison.Ordinal);
        Assert.Equal(10, payment.Code.Length);
        Assert.Equal(_clock.GetUtcNow().AddMinutes(10), payment.ExpiresAt);
        Assert.Contains("acc=123456789", payment.QrUrl, StringComparison.Ordinal);
        Assert.Contains("bank=Vietcom%26bank", payment.QrUrl, StringComparison.Ordinal);
        Assert.Contains("amount=45000", payment.QrUrl, StringComparison.Ordinal);
        Assert.Contains($"des={payment.Code}", payment.QrUrl, StringComparison.Ordinal);

        var stored = await _db.PaymentRequests.SingleAsync();
        Assert.Equal(PaymentRequestKind.CheckIn, stored.Kind);
        Assert.Equal(_shift.Id, stored.ShiftId);
        Assert.Equal(PaymentRequestStatus.Pending, stored.Status);

        var floor = (await new CatalogService(_db, _clock).GetFloorAsync(Manager, _branch.Id, default)).Value!;
        Assert.Equal(SeatLiveState.Held, floor.Zones.Single().Seats.Single(seat => seat.Id == _seat.Id).State);
        Assert.Equal(1, (await new CatalogService(_db, _clock).GetAvailabilityAsync(_branch.Id, default)).Value!.Single().Free);

        var cashCheckIn = await new SessionService(_db, _clock).CheckInAsync(
            Manager,
            new CheckInSessionRequest(_seat.Id, _plan.Id, PaymentMethod.Cash, null),
            default);
        Assert.Equal(409, cashCheckIn.Error!.Status);
    }

    [Fact]
    public async Task Extension_request_uses_the_active_sessions_extension_price_and_shift()
    {
        var session = (await new SessionService(_db, _clock).CheckInAsync(
            Manager,
            new CheckInSessionRequest(_seat.Id, _plan.Id, PaymentMethod.Cash, null),
            default)).Value!;

        var result = await Payments.CreateExtensionAsync(Manager, session.Id, default);

        Assert.True(result.IsSuccess);
        Assert.Equal(40_000, result.Value!.Amount);
        var stored = await _db.PaymentRequests.SingleAsync();
        Assert.Equal(PaymentRequestKind.Extension, stored.Kind);
        Assert.Equal(session.Id, stored.SessionId);
        Assert.Equal(_shift.Id, stored.ShiftId);
    }

    [Fact]
    public async Task Expiry_releases_hold_and_allows_a_new_request()
    {
        var original = (await Payments.CreateCheckInAsync(
            Manager,
            new CreateCheckInPaymentRequest(_seat.Id, _plan.Id, null),
            default)).Value!;
        _clock.Advance(TimeSpan.FromMinutes(11));

        Assert.Equal(1, await Payments.ExpirePendingAsync(default));
        Assert.Equal(PaymentRequestStatus.Expired, (await _db.PaymentRequests.SingleAsync()).Status);
        Assert.Equal(2, (await new CatalogService(_db, _clock).GetAvailabilityAsync(_branch.Id, default)).Value!.Single().Free);
        Assert.Equal(PaymentRequestStatus.Expired, (await Payments.GetStatusAsync(original.Code, default)).Value!.Status);

        var replacement = await Payments.CreateCheckInAsync(
            Manager,
            new CreateCheckInPaymentRequest(_seat.Id, _plan.Id, null),
            default);
        Assert.True(replacement.IsSuccess);
        Assert.Equal(2, await _db.PaymentRequests.CountAsync());
    }

    [Fact]
    public async Task Cancellation_releases_hold_and_duplicate_request_is_rejected()
    {
        var request = (await Payments.CreateCheckInAsync(
            Manager,
            new CreateCheckInPaymentRequest(_seat.Id, _plan.Id, null),
            default)).Value!;

        var duplicate = await Payments.CreateCheckInAsync(
            Manager,
            new CreateCheckInPaymentRequest(_seat.Id, _plan.Id, null),
            default);
        Assert.Equal(409, duplicate.Error!.Status);

        Assert.True((await Payments.CancelAsync(Manager, request.Code, default)).IsSuccess);
        Assert.Equal(PaymentRequestStatus.Cancelled, (await _db.PaymentRequests.SingleAsync()).Status);
        Assert.True((await Payments.CreateCheckInAsync(
            Manager,
            new CreateCheckInPaymentRequest(_seat.Id, _plan.Id, null),
            default)).IsSuccess);
    }

    [Theory]
    [InlineData("CR")]
    [InlineData("1234567890123")]
    public void Payment_options_reject_reserved_or_unusable_code_prefixes(string codePrefix)
    {
        var options = new PaymentOptions { CodePrefix = codePrefix };

        var exception = Assert.Throws<InvalidOperationException>(options.ValidateOrThrow);

        Assert.Contains("Payment__CodePrefix", exception.Message, StringComparison.Ordinal);
    }

    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now.ToUniversalTime();

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
