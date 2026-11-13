using System.Security.Cryptography;
using System.Text;
using ChonRieng.Api.Features.Common;
using ChonRieng.Api.Features.Finance;
using ChonRieng.Api.Features.Sales;
using ChonRieng.Api.Features.Scheduling;
using ChonRieng.Api.Services;
using ChonRieng.Application.Persistence;
using ChonRieng.Domain.Entities;
using ChonRieng.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ChonRieng.UnitTests.Features;

public sealed class SePayWebhookTests : IDisposable
{
    private static readonly string Secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    private const string AccountNumber = "123456789";
    private readonly TestClock _clock = new(new DateTimeOffset(2026, 11, 2, 10, 0, 0, VietnamTime.Offset));
    private readonly AppDbContext _db = new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase($"SePayWebhookTests-{Guid.NewGuid()}").Options);
    private readonly PaymentOptions _options = new()
    {
        AccountNumber = AccountNumber,
        CodePrefix = "TT",
        WebhookSecret = Secret,
        TimestampToleranceSeconds = 300,
    };

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Missing_invalid_and_stale_signatures_are_rejected()
    {
        const string body = "{}";
        var noHeaders = await SendAsync(body, null, null);
        var invalid = await SendAsync(body, Timestamp(), "sha256=00");
        var staleTimestamp = (_clock.GetUtcNow().ToUnixTimeSeconds() - 301).ToString();
        var stale = await SendAsync(body, staleTimestamp, Sign(staleTimestamp, body));

        Assert.Equal(StatusCodes.Status401Unauthorized, StatusCode(noHeaders));
        Assert.Equal(StatusCodes.Status401Unauthorized, StatusCode(invalid));
        Assert.Equal(StatusCodes.Status401Unauthorized, StatusCode(stale));
        Assert.Empty(await _db.PaymentTransactions.ToListAsync());
    }

    [Fact]
    public async Task Signed_malformed_payload_is_rejected()
    {
        const string body = "{malformed";
        var timestamp = Timestamp();

        var result = await SendAsync(body, timestamp, Sign(timestamp, body));

        Assert.Equal(StatusCodes.Status400BadRequest, StatusCode(result));
        Assert.Empty(await _db.PaymentTransactions.ToListAsync());
    }

    [Fact]
    public async Task Raw_body_tampering_invalidates_the_signature()
    {
        const string signedBody = """{"id":"tx","transactionDate":"2026-11-02 10:00:00"}""";
        const string sentBody = """{"id":"tx2","transactionDate":"2026-11-02 10:00:00"}""";
        var timestamp = Timestamp();

        var result = await SendAsync(sentBody, timestamp, Sign(timestamp, signedBody));

        Assert.Equal(StatusCodes.Status401Unauthorized, StatusCode(result));
        Assert.Empty(await _db.PaymentTransactions.ToListAsync());
    }

    [Fact]
    public async Task Valid_signature_accepts_provider_numeric_and_string_values_and_converts_vietnam_time()
    {
        const string body = """
            {
              "id": 12345,
              "transactionDate": "2026-11-02 10:00:00",
              "accountNumber": 123456789,
              "code": null,
              "content": null,
              "transferType": "out",
              "transferAmount": "12000"
            }
            """;

        var timestamp = Timestamp();
        var result = await SendAsync(body, timestamp, Sign(timestamp, body));

        Assert.Equal(StatusCodes.Status200OK, StatusCode(result));
        var recorded = await _db.PaymentTransactions.SingleAsync();
        Assert.Equal("12345", recorded.ProviderTxId);
        Assert.Equal(12_000, recorded.Amount);
        Assert.Equal(new DateTimeOffset(2026, 11, 2, 3, 0, 0, TimeSpan.Zero), recorded.OccurredAt);
        Assert.Equal(PaymentMatchStatus.Unmatched, recorded.MatchStatus);
        Assert.Contains("chuyển ra", recorded.Note, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Exact_signed_check_in_settles_once_and_duplicate_delivery_is_idempotent()
    {
        var (manager, branch, seat, plan, shift) = await SeedCheckInAsync();
        var requestResult = await new PaymentRequestService(
            _db, Options.Create(_options), _clock, new SessionService(_db, _clock), TestPaymentLog.For(_db, _clock))
            .CreateCheckInAsync(manager, new CreateCheckInPaymentRequest(seat.Id, plan.Id, null), default);
        Assert.True(requestResult.IsSuccess);
        var paymentRequest = await _db.PaymentRequests.SingleAsync();
        var body = Payload("tx-check-in-1", AccountNumber, paymentRequest.Code, 45_000);
        var timestamp = Timestamp();

        var first = await SendAsync(body, timestamp, Sign(timestamp, body));
        var retry = await SendAsync(body, timestamp, Sign(timestamp, body));

        Assert.Equal(StatusCodes.Status200OK, StatusCode(first));
        Assert.Equal(StatusCodes.Status200OK, StatusCode(retry));
        Assert.Equal(PaymentRequestStatus.Paid, paymentRequest.Status);
        Assert.Equal(shift.Id, (await _db.Bills.SingleAsync()).ShiftId);
        Assert.Equal(PaymentMethod.VietQr, (await _db.Bills.SingleAsync()).PaymentMethod);
        Assert.Equal(45_000, (await _db.Bills.SingleAsync()).VietQrPaidTotal);
        Assert.NotNull(paymentRequest.SessionId);
        var status = await new PaymentRequestService(_db, Options.Create(_options), _clock, new SessionService(_db, _clock), TestPaymentLog.For(_db, _clock))
            .GetStatusAsync(paymentRequest.Code, default);
        Assert.Equal((await _db.Bills.SingleAsync()).Code, status.Value!.BillCode);
        Assert.Equal(1, await _db.Sessions.CountAsync());
        Assert.Equal(1, await _db.Bills.CountAsync());
        Assert.Equal(1, await _db.PaymentTransactions.CountAsync());
        Assert.Equal(PaymentMatchStatus.Matched, (await _db.PaymentTransactions.SingleAsync()).MatchStatus);
        Assert.Equal(branch.Id, paymentRequest.BranchId);
    }

    [Fact]
    public async Task Wrong_account_event_is_recorded_but_does_not_settle_request()
    {
        var (manager, _, seat, plan, _) = await SeedCheckInAsync();
        await new PaymentRequestService(
            _db, Options.Create(_options), _clock, new SessionService(_db, _clock), TestPaymentLog.For(_db, _clock))
            .CreateCheckInAsync(manager, new CreateCheckInPaymentRequest(seat.Id, plan.Id, null), default);
        var paymentRequest = await _db.PaymentRequests.SingleAsync();
        var body = Payload("tx-wrong-account", "999999999", paymentRequest.Code, 45_000);
        var timestamp = Timestamp();

        var result = await SendAsync(body, timestamp, Sign(timestamp, body));

        Assert.Equal(StatusCodes.Status200OK, StatusCode(result));
        Assert.Equal(PaymentRequestStatus.Pending, paymentRequest.Status);
        Assert.Empty(await _db.Sessions.ToListAsync());
        Assert.Equal(PaymentMatchStatus.Unmatched, (await _db.PaymentTransactions.SingleAsync()).MatchStatus);
    }

    [Theory]
    [InlineData(40_000L, PaymentMatchStatus.Underpaid)]
    [InlineData(50_000L, PaymentMatchStatus.Overpaid)]
    public async Task Amount_mismatch_is_recorded_for_attention_without_settlement(long amount, PaymentMatchStatus expectedStatus)
    {
        var (manager, _, seat, plan, _) = await SeedCheckInAsync();
        await CreateCheckInRequestAsync(manager, seat, plan);
        var paymentRequest = await _db.PaymentRequests.SingleAsync();
        var body = Payload($"tx-mismatch-{amount}", AccountNumber, paymentRequest.Code, amount);
        var timestamp = Timestamp();

        var result = await SendAsync(body, timestamp, Sign(timestamp, body));

        Assert.Equal(StatusCodes.Status200OK, StatusCode(result));
        Assert.Equal(PaymentRequestStatus.NeedsAttention, paymentRequest.Status);
        Assert.Equal(expectedStatus, (await _db.PaymentTransactions.SingleAsync()).MatchStatus);
        Assert.Empty(await _db.Sessions.ToListAsync());
        Assert.Empty(await _db.Bills.ToListAsync());
    }

    [Fact]
    public async Task Expired_request_is_recorded_for_attention_without_settlement()
    {
        var (manager, _, seat, plan, _) = await SeedCheckInAsync();
        await CreateCheckInRequestAsync(manager, seat, plan);
        var paymentRequest = await _db.PaymentRequests.SingleAsync();
        _clock.Advance(TimeSpan.FromMinutes(11));
        var body = Payload("tx-expired", AccountNumber, paymentRequest.Code, 45_000);
        var timestamp = Timestamp();

        var result = await SendAsync(body, timestamp, Sign(timestamp, body));

        Assert.Equal(StatusCodes.Status200OK, StatusCode(result));
        Assert.Equal(PaymentRequestStatus.NeedsAttention, paymentRequest.Status);
        Assert.Empty(await _db.Sessions.ToListAsync());
        Assert.Empty(await _db.Bills.ToListAsync());
    }

    [Fact]
    public async Task Payment_received_after_cancel_is_marked_for_attention_and_not_settled()
    {
        var (manager, _, seat, plan, _) = await SeedCheckInAsync();
        await CreateCheckInRequestAsync(manager, seat, plan);
        var paymentRequest = await _db.PaymentRequests.SingleAsync();
        var cancelResult = await new PaymentRequestService(_db, Options.Create(_options), _clock, new SessionService(_db, _clock), TestPaymentLog.For(_db, _clock))
            .CancelAsync(manager, paymentRequest.Code, default);
        Assert.True(cancelResult.IsSuccess);
        var body = Payload("tx-after-cancel", AccountNumber, paymentRequest.Code, 45_000);
        var timestamp = Timestamp();

        var result = await SendAsync(body, timestamp, Sign(timestamp, body));

        Assert.Equal(StatusCodes.Status200OK, StatusCode(result));
        Assert.Equal(PaymentRequestStatus.NeedsAttention, paymentRequest.Status);
        Assert.Empty(await _db.Sessions.ToListAsync());
        Assert.Empty(await _db.Bills.ToListAsync());
    }

    [Fact]
    public async Task Check_in_falls_back_to_another_available_seat_of_the_same_type()
    {
        var (manager, _, seat, plan, _) = await SeedCheckInAsync();
        var fallbackSeat = new Seat { ZoneId = seat.ZoneId, Code = "A-02", Type = seat.Type };
        _db.Seats.Add(fallbackSeat);
        await _db.SaveChangesAsync();
        await CreateCheckInRequestAsync(manager, seat, plan);
        var paymentRequest = await _db.PaymentRequests.SingleAsync();
        _db.Sessions.Add(new Session
        {
            SeatId = seat.Id,
            PricePlanId = plan.Id,
            StartAt = _clock.GetUtcNow(),
            EndAt = _clock.GetUtcNow().AddHours(1),
            Status = SessionStatus.Active,
        });
        await _db.SaveChangesAsync();
        var body = Payload("tx-seat-fallback", AccountNumber, paymentRequest.Code, 45_000);
        var timestamp = Timestamp();

        var result = await SendAsync(body, timestamp, Sign(timestamp, body));

        Assert.Equal(StatusCodes.Status200OK, StatusCode(result));
        Assert.Equal(PaymentRequestStatus.Paid, paymentRequest.Status);
        Assert.Equal(fallbackSeat.Id, paymentRequest.SeatId);
        Assert.Equal(1, await _db.Sessions.CountAsync(s => s.SeatId == fallbackSeat.Id && s.Status == SessionStatus.Active));
        Assert.Equal(1, await _db.Bills.CountAsync());
    }

    [Fact]
    public async Task No_available_seat_marks_payment_for_attention_without_creating_a_sale()
    {
        var (manager, _, seat, plan, _) = await SeedCheckInAsync();
        await CreateCheckInRequestAsync(manager, seat, plan);
        var paymentRequest = await _db.PaymentRequests.SingleAsync();
        seat.Status = SeatStatus.Maintenance;
        await _db.SaveChangesAsync();
        var body = Payload("tx-no-seat", AccountNumber, paymentRequest.Code, 45_000);
        var timestamp = Timestamp();

        var result = await SendAsync(body, timestamp, Sign(timestamp, body));

        Assert.Equal(StatusCodes.Status200OK, StatusCode(result));
        Assert.Equal(PaymentRequestStatus.NeedsAttention, paymentRequest.Status);
        Assert.Empty(await _db.Sessions.ToListAsync());
        Assert.Empty(await _db.Bills.ToListAsync());
        Assert.Contains("chỗ trống", (await _db.PaymentTransactions.SingleAsync()).Note, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Vietqr_extension_updates_the_existing_bill_as_mixed_tender()
    {
        var (manager, _, seat, plan, _) = await SeedCheckInAsync();
        var sessions = new SessionService(_db, _clock);
        var activeSession = (await sessions.CheckInAsync(
            manager,
            new CheckInSessionRequest(seat.Id, plan.Id, PaymentMethod.Cash, null),
            default)).Value!;
        await new PaymentRequestService(_db, Options.Create(_options), _clock, sessions, TestPaymentLog.For(_db, _clock))
            .CreateExtensionAsync(manager, activeSession.Id, default);
        var paymentRequest = await _db.PaymentRequests.SingleAsync();
        var body = Payload("tx-extension", AccountNumber, paymentRequest.Code, 40_000);
        var timestamp = Timestamp();

        var result = await SendAsync(body, timestamp, Sign(timestamp, body));

        var bill = await _db.Bills.SingleAsync();
        Assert.Equal(StatusCodes.Status200OK, StatusCode(result));
        Assert.Equal(PaymentMethod.Mixed, bill.PaymentMethod);
        Assert.Equal(45_000, bill.CashPaidTotal);
        Assert.Equal(40_000, bill.VietQrPaidTotal);
        Assert.Equal(85_000, bill.Total);
        Assert.Single(await _db.SessionExtensions.ToListAsync());
        var closingPreview = await new FinanceService(_db, new ShiftService(_db, _clock), _clock)
            .PreviewAsync(manager, paymentRequest.ShiftId, default);
        Assert.Equal(45_000, closingPreview.Value!.ExpectedCash);
        Assert.Equal(40_000, closingPreview.Value.VietQrTotal);
    }

    [Fact]
    public async Task Cash_add_on_items_go_on_the_session_bill_with_snapshot_prices()
    {
        var (manager, branch, seat, plan, _) = await SeedCheckInAsync();
        var sessions = new SessionService(_db, _clock);
        var session = (await sessions.CheckInAsync(manager, new CheckInSessionRequest(seat.Id, plan.Id, PaymentMethod.Cash, null), default)).Value!;
        var (noodle, egg, inactive, foreign) = await SeedMenuAsync(branch);
        var addOns = new AddOnService(_db, _clock);

        var sold = await addOns.SellCashAsync(manager, session.Id,
            new AddOnSaleRequest([new(noodle.Id, 2), new(egg.Id, 1), new(noodle.Id, 1)]), default);

        Assert.True(sold.IsSuccess);
        var bill = await _db.Bills.SingleAsync();
        Assert.Equal(45_000 + 3 * 15_000 + 8_000, bill.Total);
        Assert.Equal(bill.Total, bill.CashPaidTotal);
        var lines = await _db.BillItems.OrderBy(i => i.Name).ToListAsync();
        Assert.Equal(2, lines.Count);
        Assert.Equal((3, 45_000m), (lines.Single(l => l.ProductId == noodle.Id).Quantity, lines.Single(l => l.ProductId == noodle.Id).Amount));

        // Later menu changes do not touch the sold line.
        noodle.Price = 99_000;
        await _db.SaveChangesAsync();
        Assert.Equal(15_000, (await _db.BillItems.SingleAsync(l => l.ProductId == noodle.Id)).UnitPrice);
        var view = (await sessions.ActiveAsync(manager, branch.Id, default)).Value!.Single();
        Assert.Equal(2, view.Items.Count);

        Assert.False((await addOns.SellCashAsync(manager, session.Id, new AddOnSaleRequest([new(inactive.Id, 1)]), default)).IsSuccess);
        Assert.False((await addOns.SellCashAsync(manager, session.Id, new AddOnSaleRequest([new(foreign.Id, 1)]), default)).IsSuccess);
        Assert.False((await addOns.SellCashAsync(manager, session.Id, new AddOnSaleRequest([new(egg.Id, 0)]), default)).IsSuccess);
        await sessions.CloseAsync(manager, session.Id, default);
        Assert.False((await addOns.SellCashAsync(manager, session.Id, new AddOnSaleRequest([new(egg.Id, 1)]), default)).IsSuccess);
    }

    [Fact]
    public async Task Vietqr_add_on_items_are_added_when_sepay_confirms()
    {
        var (manager, branch, seat, plan, _) = await SeedCheckInAsync();
        var sessions = new SessionService(_db, _clock);
        var session = (await sessions.CheckInAsync(manager, new CheckInSessionRequest(seat.Id, plan.Id, PaymentMethod.Cash, null), default)).Value!;
        var (noodle, egg, _, _) = await SeedMenuAsync(branch);

        var created = await new PaymentRequestService(_db, Options.Create(_options), _clock, sessions, TestPaymentLog.For(_db, _clock))
            .CreateAddOnAsync(manager, session.Id, new AddOnSaleRequest([new(noodle.Id, 1), new(egg.Id, 2)]), default);
        Assert.True(created.IsSuccess);
        Assert.Equal(31_000, created.Value!.Amount);
        Assert.Empty(await _db.BillItems.ToListAsync());

        var paymentRequest = await _db.PaymentRequests.SingleAsync();
        var body = Payload("tx-add-on", AccountNumber, paymentRequest.Code, 31_000);
        var timestamp = Timestamp();
        await SendAsync(body, timestamp, Sign(timestamp, body));

        Assert.Equal(PaymentRequestStatus.Paid, paymentRequest.Status);
        var bill = await _db.Bills.SingleAsync();
        Assert.Equal(45_000 + 31_000, bill.Total);
        Assert.Equal(31_000, bill.VietQrPaidTotal);
        Assert.Equal(PaymentMethod.Mixed, bill.PaymentMethod);
        Assert.Equal(2, await _db.BillItems.CountAsync(i => i.PaymentMethod == PaymentMethod.VietQr && i.PaymentRequestId == paymentRequest.Id));
        Assert.Equal(PaymentLogLevel.Info, (await _db.PaymentLogs.SingleAsync(l => l.Stage == PaymentLogStage.Settled)).Level);
    }

    private async Task<(Product Noodle, Product Egg, Product Inactive, Product Foreign)> SeedMenuAsync(Branch branch)
    {
        var otherBranch = new Branch { Name = "Other", Address = "y", Is24h = true, OpenTime = new TimeOnly(0, 0), CloseTime = new TimeOnly(0, 0) };
        var noodle = new Product { Name = "Mì ly", Category = "Đồ ăn", Price = 15_000 };
        var egg = new Product { BranchId = branch.Id, Name = "Trứng", Category = "Đồ ăn", Price = 8_000 };
        var inactive = new Product { Name = "Ngừng bán", Category = "Đồ ăn", Price = 1_000, IsActive = false };
        var foreign = new Product { BranchId = otherBranch.Id, Name = "Món chi nhánh khác", Category = "Đồ ăn", Price = 1_000 };
        _db.AddRange(otherBranch, noodle, egg, inactive, foreign);
        await _db.SaveChangesAsync();
        return (noodle, egg, inactive, foreign);
    }

    private async Task CreateCheckInRequestAsync(CurrentUser manager, Seat seat, PricePlan plan)
    {
        var result = await new PaymentRequestService(
            _db, Options.Create(_options), _clock, new SessionService(_db, _clock), TestPaymentLog.For(_db, _clock))
            .CreateCheckInAsync(manager, new CreateCheckInPaymentRequest(seat.Id, plan.Id, null), default);
        Assert.True(result.IsSuccess);
    }

    private async Task<(CurrentUser Manager, Branch Branch, Seat Seat, PricePlan Plan, Shift Shift)> SeedCheckInAsync()
    {
        var branch = new Branch
        {
            Name = "Branch",
            Address = "x",
            Is24h = true,
            OpenTime = new TimeOnly(0, 0),
            CloseTime = new TimeOnly(0, 0),
        };
        var zone = new Zone { BranchId = branch.Id, Name = "Zone" };
        var seat = new Seat { ZoneId = zone.Id, Code = "A-01", Type = SeatType.Single };
        var plan = new PricePlan
        {
            Name = "Plan",
            DurationMinutes = 240,
            Price = 45_000,
            ExtendPrice = 40_000,
        };
        var shift = new Shift
        {
            BranchId = branch.Id,
            Date = DateOnly.FromDateTime(_clock.GetUtcNow().ToOffset(VietnamTime.Offset).DateTime),
            Type = ShiftType.Morning,
            StartAt = _clock.GetUtcNow().AddHours(-1),
            EndAt = _clock.GetUtcNow().AddHours(7),
            RequiredStaff = 1,
            Status = ShiftStatus.Published,
        };
        _db.AddRange(branch, zone, seat, plan, shift);
        await _db.SaveChangesAsync();

        var manager = new CurrentUser(
            Guid.CreateVersion7(),
            new HashSet<string> { BusinessRoles.BranchManager },
            BranchAccessScope.ForBranches(new HashSet<Guid> { branch.Id }));
        return (manager, branch, seat, plan, shift);
    }

    private async Task<IResult> SendAsync(string body, string? timestamp, string? signature)
    {
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        if (timestamp is not null)
        {
            context.Request.Headers["X-SePay-Timestamp"] = timestamp;
        }

        if (signature is not null)
        {
            context.Request.Headers["X-SePay-Signature"] = signature;
        }

        var service = new SePayWebhookService(
            _db,
            Options.Create(_options),
            _clock,
            new SessionService(_db, _clock),
            TestPaymentLog.For(_db, _clock),
            NullLogger<SePayWebhookService>.Instance);
        return await service.HandleAsync(context.Request, default);
    }

    private string Timestamp() => _clock.GetUtcNow().ToUnixTimeSeconds().ToString();

    private static string Sign(string timestamp, string body)
    {
        var signedBytes = Encoding.UTF8.GetBytes($"{timestamp}.{body}");
        var signature = HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret), signedBytes);
        return $"sha256={Convert.ToHexString(signature).ToLowerInvariant()}";
    }

    [Fact]
    public async Task Paid_request_leaves_a_full_trace_from_creation_to_settlement()
    {
        var (manager, _, seat, plan, _) = await SeedCheckInAsync();
        var payments = new PaymentRequestService(_db, Options.Create(_options), _clock, new SessionService(_db, _clock), TestPaymentLog.For(_db, _clock));
        await payments.CreateCheckInAsync(manager, new CreateCheckInPaymentRequest(seat.Id, plan.Id, null), default);
        var paymentRequest = await _db.PaymentRequests.SingleAsync();
        await payments.GetStatusAsync(paymentRequest.Code, default);
        await payments.GetStatusAsync(paymentRequest.Code, default); // throttled: same status within 30 s
        _clock.Advance(TimeSpan.FromSeconds(20));
        var body = Payload("tx-trace", AccountNumber, paymentRequest.Code, 45_000);
        var timestamp = Timestamp();
        await SendAsync(body, timestamp, Sign(timestamp, body));
        await payments.GetStatusAsync(paymentRequest.Code, default);

        // The test clock is frozen, so rows written in the same step share a timestamp; compare the steps, not their order.
        var stages = await _db.PaymentLogs.AsNoTracking().Where(l => l.PaymentCode == paymentRequest.Code || l.PaymentRequestId == paymentRequest.Id)
            .Select(l => l.Stage).ToListAsync();
        Assert.Equal(
            new[]
            {
                PaymentLogStage.RequestCreated, PaymentLogStage.StatusPolled, PaymentLogStage.TransactionRecorded,
                PaymentLogStage.CodeMatched, PaymentLogStage.Settled, PaymentLogStage.StatusPolled,
            }.Order(),
            stages.Order());
        var settled = await _db.PaymentLogs.SingleAsync(l => l.Stage == PaymentLogStage.Settled);
        Assert.Equal((await _db.Bills.SingleAsync()).Code, settled.BillCode);
        Assert.Equal(20_000, settled.ElapsedMs);
        Assert.Equal(1, await _db.PaymentLogs.CountAsync(l => l.Stage == PaymentLogStage.WebhookReceived));
    }

    [Fact]
    public async Task Rejected_callbacks_are_logged_with_the_reason()
    {
        var body = Payload("tx-rejected", AccountNumber, "TTABCDEFGH", 10_000);
        var staleTimestamp = (_clock.GetUtcNow().ToUnixTimeSeconds() - 900).ToString();
        await SendAsync(body, staleTimestamp, Sign(staleTimestamp, body));
        var timestamp = Timestamp();
        await SendAsync(body, timestamp, "sha256=" + new string('0', 64));
        await SendAsync(body, null, null);

        var rejected = await _db.PaymentLogs.Where(l => l.Stage == PaymentLogStage.WebhookRejected).OrderBy(l => l.OccurredAt).ThenBy(l => l.Id).ToListAsync();
        Assert.Equal(3, rejected.Count);
        Assert.All(rejected, l => Assert.Equal(PaymentLogLevel.Error, l.Level));
        Assert.Contains("stale_timestamp", rejected[0].Detail);
        Assert.Contains("bad_signature", rejected[1].Detail);
        Assert.Contains("missing_headers", rejected[2].Detail);
        Assert.DoesNotContain(Secret, string.Join("", rejected.Select(l => l.Detail)));
        Assert.Empty(await _db.PaymentTransactions.ToListAsync());
    }

    [Fact]
    public async Task Wrong_code_field_falls_back_to_the_code_in_transfer_content()
    {
        var (manager, _, seat, plan, _) = await SeedCheckInAsync();
        await new PaymentRequestService(_db, Options.Create(_options), _clock, new SessionService(_db, _clock), TestPaymentLog.For(_db, _clock))
            .CreateCheckInAsync(manager, new CreateCheckInPaymentRequest(seat.Id, plan.Id, null), default);
        var paymentRequest = await _db.PaymentRequests.SingleAsync();
        var body = $$"""
            {
              "id": "tx-fallback",
              "transactionDate": "2026-11-02 10:00:00",
              "accountNumber": "{{AccountNumber}}",
              "code": "TT1234",
              "content": "NGUYEN VAN A CK {{paymentRequest.Code}} FT123",
              "transferType": "in",
              "transferAmount": 45000
            }
            """;
        var timestamp = Timestamp();
        await SendAsync(body, timestamp, Sign(timestamp, body));

        Assert.Equal(PaymentRequestStatus.Paid, paymentRequest.Status);
        var matched = await _db.PaymentLogs.SingleAsync(l => l.Stage == PaymentLogStage.CodeMatched);
        Assert.Contains("\"matchedBy\":\"content\"", matched.Detail);
    }

    private static string Payload(string id, string accountNumber, string code, decimal amount) =>
        $$"""
          {
            "id": "{{id}}",
            "transactionDate": "2026-11-02 10:00:00",
            "accountNumber": "{{accountNumber}}",
            "code": "{{code}}",
            "content": "{{code}}",
            "transferType": "in",
            "transferAmount": {{amount}}
          }
          """;

    private static int? StatusCode(IResult result) => (result as IStatusCodeHttpResult)?.StatusCode;

    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now.ToUniversalTime();

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
