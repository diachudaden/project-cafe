using System.Diagnostics;
using System.Text.Json;
using ChonRieng.Domain.Entities;
using ChonRieng.Infrastructure.Persistence;

namespace ChonRieng.Api.Features.Sales;

/// <summary>What happened at one payment step; only the fields that are known need to be set.</summary>
public sealed record PaymentLogEntry(PaymentLogStage Stage, string Message)
{
    public PaymentLogLevel? Level { get; init; }
    public PaymentRequest? Request { get; init; }
    public PaymentTransaction? Transaction { get; init; }
    public string? PaymentCode { get; init; }
    public string? ProviderTxId { get; init; }
    public Guid? BillId { get; init; }
    public string? BillCode { get; init; }
    public Guid? BranchId { get; init; }
    public decimal? Amount { get; init; }
    /// <summary>Anonymous object serialized to JSON; keep it small and free of secrets or transfer content.</summary>
    public object? Detail { get; init; }
}

/// <summary>
/// Writes the payment trace (<see cref="PaymentLog"/>). Each write uses its own DbContext so a row survives
/// when the payment's own transaction rolls back, and a failing write is swallowed: tracing must never
/// break or slow down a payment.
/// </summary>
public sealed class PaymentLogService(
    IServiceScopeFactory scopeFactory,
    IHttpContextAccessor httpContextAccessor,
    TimeProvider clock,
    ILogger<PaymentLogService> logger)
{
    private static readonly JsonSerializerOptions DetailJson = new(JsonSerializerDefaults.Web);

    public static PaymentLogLevel DefaultLevel(PaymentLogStage stage) => stage switch
    {
        PaymentLogStage.WebhookRejected or PaymentLogStage.SettlementFailed or PaymentLogStage.Exception => PaymentLogLevel.Error,
        PaymentLogStage.TransactionIgnored or PaymentLogStage.TransactionUnmatched or PaymentLogStage.AmountMismatch
            or PaymentLogStage.LatePayment or PaymentLogStage.AlreadyPaid or PaymentLogStage.Expired => PaymentLogLevel.Warning,
        _ => PaymentLogLevel.Info,
    };

    public async Task WriteAsync(PaymentLogEntry entry)
    {
        try
        {
            var now = clock.GetUtcNow();
            var request = entry.Request;
            var transaction = entry.Transaction;
            var log = new PaymentLog
            {
                OccurredAt = now,
                Stage = entry.Stage,
                Level = entry.Level ?? DefaultLevel(entry.Stage),
                PaymentRequestId = request?.Id ?? transaction?.PaymentRequestId,
                PaymentCode = Limit(entry.PaymentCode ?? request?.Code, 20),
                PaymentTransactionId = transaction?.Id,
                ProviderTxId = Limit(entry.ProviderTxId ?? transaction?.ProviderTxId, 128),
                BillId = entry.BillId ?? request?.BillId,
                BillCode = Limit(entry.BillCode, 16),
                BranchId = entry.BranchId ?? request?.BranchId,
                Amount = entry.Amount ?? request?.Amount ?? transaction?.Amount,
                Message = Limit(entry.Message, 500) ?? "",
                Detail = entry.Detail is null ? null : Limit(JsonSerializer.Serialize(entry.Detail, DetailJson), 2000),
                TraceId = Limit(httpContextAccessor.HttpContext?.TraceIdentifier ?? Activity.Current?.TraceId.ToString(), 64),
                ElapsedMs = request is { CreatedAt: var created } && created != default
                    ? (long)Math.Max(0, (now - created).TotalMilliseconds)
                    : null,
            };

            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.PaymentLogs.Add(log);
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not write payment log {Stage}.", entry.Stage);
        }
    }

    private static string? Limit(string? value, int max) =>
        value is null ? null : value.Length <= max ? value : value[..max];
}
