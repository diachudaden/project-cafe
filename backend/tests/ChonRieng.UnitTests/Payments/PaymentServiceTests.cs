using ChonRieng.Application.Payments;
using ChonRieng.Application.Payments.Sepay;
using ChonRieng.Domain.Payments;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ChonRieng.UnitTests.Payments;

public class PaymentServiceTests
{
    private const string Account = "0123456789";
    private static readonly DateTimeOffset Now = new(2026, 10, 18, 8, 0, 0, TimeSpan.Zero); // 15:00 giờ VN

    private readonly InMemoryPaymentStore _store = new();
    private readonly RecordingNotifier _notifier = new();
    private readonly FixedTime _clock = new(Now);
    private readonly PaymentOptions _options = new()
    {
        Bank = "MBBank", AccountNumber = Account, AccountHolder = "NGUYEN VAN A",
        CodePrefix = "CR", WebhookSecret = "s", SeatHoldMinutes = 10,
    };

    private PaymentService Service()
    {
        var opt = Options.Create(_options);
        return new PaymentService(
            _store,
            [new VietQrProvider(opt)],
            new SeatAllocator(_store, new LowestSortOrderSeatStrategy()),
            _notifier,
            opt,
            _clock,
            NullLogger<PaymentService>.Instance);
    }

    private static SepayWebhookPayload Payload(long id, long amount, string? code, string? content = null, string account = Account, string type = "in") =>
        new(id, "MBBank", "2026-10-18 15:00:05", account, "", code, content ?? $"{code} chuyen tien", type, "", amount, 0, "FT1");

    private Task<WebhookOutcome> Webhook(SepayWebhookPayload p) => Service().HandleSepayAsync(p, "{}", CancellationToken.None);

    // ------------------------------------------------------------ Tạo QR

    [Fact]
    public async Task CreateQr_builds_vietqr_url_and_holds_requested_seat()
    {
        _store.Bills.Add(new FakeBill { Id = 1024, RequestedSeatId = 5 });
        _store.Seats.Add(new FakeSeat { Id = 5, Code = "A5" });

        var result = await Service().CreateQrAsync(1024, default);

        Assert.True(result.IsSuccess);
        var qr = result.Value!;
        Assert.Equal("CR1024", qr.Code);
        Assert.Equal(25_000, qr.Amount);
        Assert.Equal("https://vietqr.app/img?acc=0123456789&bank=MBBank&amount=25000&des=CR1024&template=compact", qr.QrUrl);
        Assert.Equal("******6789", qr.AccountNumber);

        var seat = _store.Seats[0];
        Assert.Equal(1024, seat.HeldForBillId);
        Assert.Equal(Now.UtcDateTime.AddMinutes(10), seat.HeldUntil);
    }

    [Fact]
    public async Task CreateQr_adds_description_prefix_for_vietinbank()
    {
        _options.DescriptionPrefix = "SEVQR";
        _store.Bills.Add(new FakeBill { Id = 1, RequiresSeat = false });

        var qr = (await Service().CreateQrAsync(1, default)).Value!;

        Assert.Contains("des=SEVQR%20CR1", qr.QrUrl);
        Assert.Equal("CR1", qr.Code);
    }

    [Fact]
    public async Task CreateQr_rejects_seat_held_by_another_bill()
    {
        _store.Bills.Add(new FakeBill { Id = 2, RequestedSeatId = 5 });
        var seat = new FakeSeat { Id = 5 };
        seat.Hold(billId: 1, Now.UtcDateTime.AddMinutes(5));
        _store.Seats.Add(seat);

        var result = await Service().CreateQrAsync(2, default);

        Assert.Equal("SEAT_TAKEN", result.Error?.Code);
    }

    [Fact]
    public async Task CreateQr_takes_seat_whose_hold_expired()
    {
        _store.Bills.Add(new FakeBill { Id = 2, RequestedSeatId = 5 });
        var seat = new FakeSeat { Id = 5 };
        seat.Hold(billId: 1, Now.UtcDateTime.AddMinutes(-1));
        _store.Seats.Add(seat);

        Assert.True((await Service().CreateQrAsync(2, default)).IsSuccess);
        Assert.Equal(2, seat.HeldForBillId);
    }

    [Fact]
    public async Task CreateQr_twice_extends_hold()
    {
        _store.Bills.Add(new FakeBill { Id = 3, RequestedSeatId = 5 });
        _store.Seats.Add(new FakeSeat { Id = 5 });
        await Service().CreateQrAsync(3, default);

        _clock.Now = Now.AddMinutes(8);
        Assert.True((await Service().CreateQrAsync(3, default)).IsSuccess);
        Assert.Equal(Now.UtcDateTime.AddMinutes(18), _store.Seats[0].HeldUntil);
    }

    [Fact]
    public async Task CreateQr_rejects_paid_bill()
    {
        var bill = new FakeBill { Id = 4, RequiresSeat = false };
        bill.MarkPaid(PaymentMethod.Cash, 25_000, Now.UtcDateTime);
        _store.Bills.Add(bill);

        Assert.Equal("BILL_NOT_PAYABLE", (await Service().CreateQrAsync(4, default)).Error?.Code);
    }

    // ------------------------------------------------------------ Webhook

    [Fact]
    public async Task Webhook_exact_amount_pays_bill_assigns_held_seat_and_writes_ledger()
    {
        _store.Bills.Add(new FakeBill { Id = 1024, RequestedSeatId = 5 });
        _store.Seats.Add(new FakeSeat { Id = 5, Code = "A5" });
        await Service().CreateQrAsync(1024, default);

        var outcome = await Webhook(Payload(9001, 25_000, "CR1024"));

        Assert.Equal(WebhookOutcome.Applied, outcome);
        Assert.True(_store.Bills[0].IsPaid);
        Assert.Equal(PaymentMethod.VietQr, _store.Bills[0].PaidWith);
        Assert.Contains((1024, 5), _store.Sessions);

        var entry = Assert.Single(_store.Ledger);
        Assert.Equal(LedgerDirection.In, entry.Direction);
        Assert.Equal(25_000, entry.Amount);
        Assert.Equal(9001, entry.PaymentTransactionId);
        Assert.Equal(7, entry.ShiftId);
        Assert.Null(entry.CreatedBy);
        Assert.Equal(new DateTime(2026, 10, 18, 8, 0, 5, DateTimeKind.Utc), entry.OccurredAtUtc); // 15:00:05 VN

        Assert.Equal(PaymentMatchStatus.Matched, _store.Transactions.Single().MatchStatus);
        Assert.Single(_notifier.Of<PaymentReceivedEvent>());
        var assigned = Assert.Single(_notifier.Of<SeatAssignedEvent>());
        Assert.Equal("A5", assigned.SeatCode);
        Assert.False(assigned.IsDefault);
    }

    [Fact]
    public async Task Same_webhook_three_times_writes_one_ledger_entry()
    {
        _store.Bills.Add(new FakeBill { Id = 1, RequiresSeat = false });
        var p = Payload(9002, 25_000, "CR1");

        Assert.Equal(WebhookOutcome.Applied, await Webhook(p));
        Assert.Equal(WebhookOutcome.AlreadyProcessed, await Webhook(p));
        Assert.Equal(WebhookOutcome.AlreadyProcessed, await Webhook(p));

        Assert.Single(_store.Ledger);
        Assert.Single(_store.Transactions);
    }

    [Fact]
    public async Task Parallel_duplicate_detected_at_save_is_treated_as_processed()
    {
        _store.Bills.Add(new FakeBill { Id = 1, RequiresSeat = false });
        _store.NextSaveOutcomes.Enqueue(SaveOutcome.DuplicateKey);

        Assert.Equal(WebhookOutcome.AlreadyProcessed, await Webhook(Payload(9003, 25_000, "CR1")));
        Assert.Empty(_store.Ledger);
    }

    [Fact]
    public async Task Underpaid_keeps_bill_pending_and_raises_attention()
    {
        _store.Bills.Add(new FakeBill { Id = 1, RequiresSeat = false });

        Assert.Equal(WebhookOutcome.NeedsAttention, await Webhook(Payload(9004, 20_000, "CR1")));

        Assert.False(_store.Bills[0].IsPaid);
        Assert.Empty(_store.Ledger);
        Assert.Equal(PaymentMatchStatus.Underpaid, _store.Transactions.Single().MatchStatus);
        Assert.Equal("Underpaid", Assert.Single(_notifier.Of<PaymentNeedsAttentionEvent>()).Reason);
    }

    [Fact]
    public async Task Overpaid_counts_as_paid_and_records_real_amount()
    {
        _store.Bills.Add(new FakeBill { Id = 1, RequiresSeat = false });

        Assert.Equal(WebhookOutcome.Applied, await Webhook(Payload(9005, 30_000, "CR1")));

        Assert.Equal(30_000, _store.Ledger.Single().Amount);
        Assert.Equal(PaymentMatchStatus.Overpaid, _store.Transactions.Single().MatchStatus);
    }

    [Fact]
    public async Task Second_payment_for_paid_bill_is_duplicate()
    {
        _store.Bills.Add(new FakeBill { Id = 1, RequiresSeat = false });
        await Webhook(Payload(9006, 25_000, "CR1"));

        Assert.Equal(WebhookOutcome.NeedsAttention, await Webhook(Payload(9007, 25_000, "CR1")));

        Assert.Single(_store.Ledger);
        Assert.Equal(PaymentMatchStatus.Duplicate, _store.Transactions.Single(t => t.SepayId == 9007).MatchStatus);
    }

    [Fact]
    public async Task Missing_code_falls_back_to_content()
    {
        _store.Bills.Add(new FakeBill { Id = 77, RequiresSeat = false });

        Assert.Equal(WebhookOutcome.Applied, await Webhook(Payload(9008, 25_000, code: null, content: "NGUYEN VAN A CK CR77")));
    }

    [Fact]
    public async Task No_code_anywhere_is_unmatched()
    {
        Assert.Equal(WebhookOutcome.NeedsAttention, await Webhook(Payload(9009, 25_000, code: null, content: "chuyen tien cafe")));
        Assert.Equal(PaymentMatchStatus.Unmatched, _store.Transactions.Single().MatchStatus);
    }

    [Fact]
    public async Task Outgoing_and_other_accounts_are_ignored()
    {
        Assert.Equal(WebhookOutcome.IgnoredNotIncoming, await Webhook(Payload(9010, 25_000, "CR1", type: "out")));
        Assert.Equal(WebhookOutcome.IgnoredWrongAccount, await Webhook(Payload(9011, 25_000, "CR1", account: "999")));
        Assert.Empty(_store.Transactions);
    }

    [Fact]
    public async Task Bad_payload_is_rejected_without_saving()
    {
        var p = Payload(9012, 25_000, "CR1") with { TransactionDate = "18/10/2026" };
        Assert.Equal(WebhookOutcome.InvalidPayload, await Webhook(p));
        Assert.Empty(_store.Transactions);
    }

    // ------------------------------------------------------------ Xếp chỗ

    [Fact]
    public async Task Requested_seat_taken_after_hold_expired_gets_default_seat()
    {
        _store.Bills.Add(new FakeBill { Id = 1, RequestedSeatId = 5 });
        _store.Seats.Add(new FakeSeat { Id = 5, Code = "A5", Occupied = true }); // người khác đã ngồi
        _store.Seats.Add(new FakeSeat { Id = 9, Code = "B9", SortOrder = 2 });
        _store.Seats.Add(new FakeSeat { Id = 8, Code = "B8", SortOrder = 1 });
        _store.Seats.Add(new FakeSeat { Id = 3, Code = "G3", SeatType = "Group", SortOrder = 0 });

        await Webhook(Payload(9013, 25_000, "CR1"));

        var assigned = Assert.Single(_notifier.Of<SeatAssignedEvent>());
        Assert.Equal("B8", assigned.SeatCode); // cùng loại Single, SortOrder nhỏ nhất
        Assert.True(assigned.IsDefault);
    }

    [Fact]
    public async Task Requested_seat_released_but_still_free_is_reused()
    {
        _store.Bills.Add(new FakeBill { Id = 1, RequestedSeatId = 5 });
        _store.Seats.Add(new FakeSeat { Id = 5, Code = "A5", SortOrder = 9 });
        _store.Seats.Add(new FakeSeat { Id = 6, Code = "A6", SortOrder = 1 });

        await Webhook(Payload(9014, 25_000, "CR1"));

        Assert.Equal("A5", Assert.Single(_notifier.Of<SeatAssignedEvent>()).SeatCode);
    }

    [Fact]
    public async Task No_seat_left_still_takes_payment_and_flags_bill()
    {
        _store.Bills.Add(new FakeBill { Id = 1 });

        Assert.Equal(WebhookOutcome.Applied, await Webhook(Payload(9015, 25_000, "CR1")));

        Assert.True(_store.Bills[0].IsPaid);
        Assert.Equal("NO_SEAT", _store.Bills[0].AttentionReason);
        Assert.Single(_store.Ledger);
        Assert.Equal("NO_SEAT", Assert.Single(_notifier.Of<PaymentNeedsAttentionEvent>()).Reason);
    }

    // ------------------------------------------------------------ Thủ công & tiền mặt

    [Fact]
    public async Task Manager_assigns_unmatched_transaction_to_bill()
    {
        _store.Bills.Add(new FakeBill { Id = 1, RequiresSeat = false });
        await Webhook(Payload(9016, 25_000, code: null, content: "ck tien cafe"));

        var result = await Service().AssignTransactionAsync(9016, billId: 1, staffId: 42, "Khách xoá mã", default);

        Assert.True(result.IsSuccess);
        Assert.True(_store.Bills[0].IsPaid);
        var tx = _store.Transactions.Single();
        Assert.Equal(PaymentMatchStatus.ManuallyAssigned, tx.MatchStatus);
        Assert.False(tx.NeedsAttention);
        Assert.Equal(42, _store.Ledger.Single().CreatedBy);

        var again = await Service().AssignTransactionAsync(9016, 1, 42, null, default);
        Assert.Equal("TRANSACTION_RESOLVED", again.Error?.Code);
    }

    [Fact]
    public async Task Cash_payment_writes_cash_ledger_with_shift()
    {
        _store.Bills.Add(new FakeBill { Id = 1, RequiresSeat = false });

        var result = await Service().MarkPaidCashAsync(1, staffId: 3, default);

        Assert.True(result.IsSuccess);
        var entry = _store.Ledger.Single();
        Assert.Equal(PaymentMethod.Cash, entry.Method);
        Assert.Equal(7, entry.ShiftId);
        Assert.Equal(3, entry.CreatedBy);
    }

    // ------------------------------------------------------------ Job nhả ghế

    [Fact]
    public async Task Expired_holds_are_released_and_announced()
    {
        var seat = new FakeSeat { Id = 5 };
        seat.Hold(billId: 1, Now.UtcDateTime.AddMinutes(-1));
        _store.Seats.Add(seat);
        var fresh = new FakeSeat { Id = 6 };
        fresh.Hold(billId: 2, Now.UtcDateTime.AddMinutes(5));
        _store.Seats.Add(fresh);

        await new ReleaseExpiredSeatHoldsJob(_store, _notifier, _clock, NullLogger<ReleaseExpiredSeatHoldsJob>.Instance).RunAsync(default);

        Assert.Null(seat.HeldForBillId);
        Assert.Equal(2, fresh.HeldForBillId);
        var evt = Assert.Single(_notifier.Of<SeatHoldExpiredEvent>());
        Assert.Equal((1, 5), (evt.BillId, evt.SeatId));
    }
}
