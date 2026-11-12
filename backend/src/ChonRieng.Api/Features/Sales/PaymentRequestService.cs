using System.Globalization;
using ChonRieng.Api.Features.Common;
using ChonRieng.Api.Services;
using ChonRieng.Domain.Entities;
using ChonRieng.Infrastructure.Persistence;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ChonRieng.Api.Features.Sales;

public sealed class PaymentRequestService(
    AppDbContext db,
    IOptions<PaymentOptions> options,
    TimeProvider clock,
    SessionService sessions)
{
    private readonly PaymentOptions _options = options.Value;

    public async Task<Result<CreatePaymentRequestResponse>> CreateCheckInAsync(
        CurrentUser user,
        CreateCheckInPaymentRequest request,
        CancellationToken ct)
    {
        var context = await sessions.PrepareCheckInPaymentAsync(user, request.SeatId, request.PricePlanId, request.CustomerId, ct);
        if (context.Error is not null)
        {
            return context.Error;
        }

        await ExpirePendingForSeatAsync(request.SeatId, ct);
        context = await sessions.PrepareCheckInPaymentAsync(user, request.SeatId, request.PricePlanId, request.CustomerId, ct);
        if (context.Error is not null)
        {
            return context.Error;
        }

        return await CreateAsync(user, context.Value!, ct);
    }

    public async Task<Result<CreatePaymentRequestResponse>> CreateExtensionAsync(CurrentUser user, Guid sessionId, CancellationToken ct)
    {
        var context = await sessions.PrepareExtensionPaymentAsync(user, sessionId, ct);
        if (context.Error is not null)
        {
            return context.Error;
        }

        await ExpirePendingForSeatAsync(context.Value!.SeatId, ct);
        context = await sessions.PrepareExtensionPaymentAsync(user, sessionId, ct);
        if (context.Error is not null)
        {
            return context.Error;
        }

        return await CreateAsync(user, context.Value!, ct);
    }

    public async Task<Result<PaymentRequestStatusResponse>> GetStatusAsync(string code, CancellationToken ct)
    {
        var normalizedCode = code.Trim().ToUpperInvariant();
        var paymentRequest = await db.PaymentRequests
            .Include(p => p.Bill)
            .SingleOrDefaultAsync(p => p.Code == normalizedCode, ct);
        if (paymentRequest is null)
        {
            return ServiceError.NotFound("Không tìm thấy yêu cầu thanh toán.");
        }

        ExpireIfOverdue(paymentRequest);
        await db.SaveChangesAsync(ct);
        return new PaymentRequestStatusResponse(
            paymentRequest.Code,
            paymentRequest.Amount,
            paymentRequest.Status,
            paymentRequest.ExpiresAt,
            paymentRequest.PaidAt,
            paymentRequest.Bill?.Code);
    }

    public async Task<Result<Done>> CancelAsync(CurrentUser user, string code, CancellationToken ct)
    {
        var normalizedCode = code.Trim().ToUpperInvariant();
        var paymentRequest = await db.PaymentRequests.SingleOrDefaultAsync(p => p.Code == normalizedCode, ct);
        if (paymentRequest is null || !user.CanAccessBranch(paymentRequest.BranchId))
        {
            return ServiceError.NotFound("Không tìm thấy yêu cầu thanh toán.");
        }

        if (paymentRequest.CreatedByUserId != user.UserId && !user.CanManageBranch(paymentRequest.BranchId))
        {
            return ServiceError.NotFound("Không tìm thấy yêu cầu thanh toán.");
        }

        ExpireIfOverdue(paymentRequest);
        if (paymentRequest.Status != PaymentRequestStatus.Pending)
        {
            return ServiceError.Conflict("Yêu cầu không còn chờ thanh toán", "Chỉ hủy được yêu cầu đang chờ.");
        }

        paymentRequest.Status = PaymentRequestStatus.Cancelled;
        await db.SaveChangesAsync(ct);
        return new Done();
    }

    public async Task<int> ExpirePendingAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var expiredRequests = await db.PaymentRequests
            .Where(p => p.Status == PaymentRequestStatus.Pending && p.ExpiresAt <= now)
            .ToListAsync(ct);

        foreach (var paymentRequest in expiredRequests)
        {
            paymentRequest.Status = PaymentRequestStatus.Expired;
        }

        if (expiredRequests.Count > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        return expiredRequests.Count;
    }

    private async Task<Result<CreatePaymentRequestResponse>> CreateAsync(
        CurrentUser user,
        PaymentRequestContext context,
        CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var paymentRequest = new PaymentRequest
        {
            Code = await GenerateUniqueCodeAsync(ct),
            Kind = context.Kind,
            BranchId = context.BranchId,
            SeatId = context.SeatId,
            PricePlanId = context.PricePlanId,
            CustomerId = context.CustomerId,
            SessionId = context.SessionId,
            ShiftId = context.ShiftId,
            Amount = context.Amount,
            Status = PaymentRequestStatus.Pending,
            ExpiresAt = now.AddMinutes(_options.HoldMinutes),
            CreatedByUserId = user.UserId,
            CreatedAt = now,
        };

        db.PaymentRequests.Add(paymentRequest);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception) when (exception.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 })
        {
            db.Entry(paymentRequest).State = EntityState.Detached;
            return ServiceError.Conflict("Chỗ đã được giữ", "Một thiết bị khác vừa tạo yêu cầu thanh toán cho chỗ này.");
        }

        return new CreatePaymentRequestResponse(
            paymentRequest.Code,
            paymentRequest.Amount,
            BuildQrUrl(paymentRequest.Amount, paymentRequest.Code),
            paymentRequest.ExpiresAt);
    }

    private async Task ExpirePendingForSeatAsync(Guid seatId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var expired = await db.PaymentRequests
            .Where(p => p.SeatId == seatId && p.Status == PaymentRequestStatus.Pending && p.ExpiresAt <= now)
            .ToListAsync(ct);
        if (expired.Count == 0)
        {
            return;
        }

        foreach (var paymentRequest in expired)
        {
            paymentRequest.Status = PaymentRequestStatus.Expired;
        }

        await db.SaveChangesAsync(ct);
    }

    private void ExpireIfOverdue(PaymentRequest paymentRequest)
    {
        if (paymentRequest.Status == PaymentRequestStatus.Pending && paymentRequest.ExpiresAt <= clock.GetUtcNow())
        {
            paymentRequest.Status = PaymentRequestStatus.Expired;
        }
    }

    private async Task<string> GenerateUniqueCodeAsync(CancellationToken ct)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var candidate = Loyalty.NewPaymentCode(_options.CodePrefix);
            if (!await db.PaymentRequests.AnyAsync(r => r.Code == candidate, ct))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Không thể tạo mã thanh toán duy nhất.");
    }

    private string BuildQrUrl(decimal amount, string code)
    {
        var query = new Dictionary<string, string?>
        {
            ["acc"] = _options.AccountNumber,
            ["bank"] = _options.Bank,
            ["amount"] = amount.ToString("0", CultureInfo.InvariantCulture),
            ["des"] = code,
        };

        return QueryHelpers.AddQueryString(_options.QrImageBaseUrl, query);
    }

}

public sealed record CreateCheckInPaymentRequest(
    Guid SeatId,
    Guid PricePlanId,
    Guid? CustomerId);

public sealed record CreatePaymentRequestResponse(string Code, decimal Amount, string QrUrl, DateTimeOffset ExpiresAt);

public sealed record PaymentRequestStatusResponse(
    string Code,
    decimal Amount,
    PaymentRequestStatus Status,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? PaidAt,
    string? BillCode);
