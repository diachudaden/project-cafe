using ChonRieng.Domain.Payments;

namespace ChonRieng.Application.Payments;

// ---- Sự kiện realtime (tên property = tên field JSON phía frontend, camelCase) ----

public sealed record PaymentReceivedEvent(int BillId, long Amount, PaymentMethod Method, DateTime PaidAtUtc);

public sealed record SeatAssignedEvent(int BillId, int SeatId, string SeatCode, bool IsDefault);

public sealed record PaymentNeedsAttentionEvent(long? TransactionId, int? BillId, string Reason, long Amount);

public sealed record SeatHoldExpiredEvent(int BillId, int SeatId);

// ---- DTO trả về API ----

public sealed record PaymentQrDto(int BillId, string Code, long Amount, string QrUrl, DateTime ExpiresAtUtc,
                                  string Bank, string AccountNumber, string AccountHolder);

public sealed record PaymentResultDto(int BillId, long Amount, PaymentMethod Method, int? SeatId, string? SeatCode,
                                      bool SeatIsDefault, bool NeedsAttention);

public sealed record PaymentTransactionDto(
    long Id, long Amount, string Gateway, string Content, string? Code, DateTime TransactionAtUtc,
    PaymentMatchStatus MatchStatus, int? BillId, string? Note, bool NeedsAttention);

public sealed record LedgerEntryDto(
    long Id, int BranchId, DateTime OccurredAtUtc, LedgerDirection Direction, long Amount, long SignedAmount,
    LedgerCategory Category, PaymentMethod Method, int? BillId, long? PaymentTransactionId, int? ShiftId,
    int? CreatedBy, string? Note, long? ReversalOfId);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public enum SummaryPeriod
{
    Day,
    Week,
    Month,
    Quarter,
    Year,
}

public sealed record CategoryTotal(LedgerCategory Category, LedgerDirection Direction, long Total);

public sealed record PeriodSummary(
    DateOnly PeriodStart, DateOnly PeriodEnd, string Label,
    long TotalIn, long TotalOut, long Net, IReadOnlyList<CategoryTotal> ByCategory);

public sealed record LedgerSummaryDto(
    SummaryPeriod Period, DateOnly From, DateOnly To,
    long TotalIn, long TotalOut, long Net, IReadOnlyList<PeriodSummary> Periods);

// ---- Request ----

public sealed record AddExpenseRequest(int BranchId, long Amount, LedgerCategory Category, PaymentMethod Method,
                                       DateTime? OccurredAtUtc, string? Note);

public sealed record ReverseLedgerRequest(string Reason);

public sealed record AssignTransactionRequest(int BillId, string? Note);

public sealed record DismissTransactionRequest(string Note);

public static class PaymentMappings
{
    public static LedgerEntryDto ToDto(this LedgerEntry e) => new(
        e.Id, e.BranchId, e.OccurredAtUtc, e.Direction, e.Amount, e.SignedAmount, e.Category, e.Method,
        e.BillId, e.PaymentTransactionId, e.ShiftId, e.CreatedBy, e.Note, e.ReversalOfId);

    public static PaymentTransactionDto ToDto(this PaymentTransaction t) => new(
        t.SepayId, t.Amount, t.Gateway, t.Content, t.Code, t.TransactionAtUtc, t.MatchStatus, t.BillId, t.Note,
        t.NeedsAttention);
}
