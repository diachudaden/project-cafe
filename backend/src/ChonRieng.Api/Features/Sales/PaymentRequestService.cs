using System.ComponentModel;
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
    SessionService sessions,
    PaymentLogService paymentLog,
    AddOnService? addOns = null)
{
    /// <summary>POS polls every few seconds; a poll is logged on status change or at most this often.</summary>
    private static readonly TimeSpan PollLogInterval = TimeSpan.FromSeconds(30);

    private readonly PaymentOptions _options = options.Value;

    public async Task<Result<CreatePaymentRequestResponse>> CreateCheckInAsync(
        CurrentUser user,
        CreateCheckInPaymentRequest request,
        CancellationToken ct)
    {
        var context = await sessions.PrepareCheckInPaymentAsync(user, request.SeatId, request.PricePlanId, request.CustomerId, ct, request.Items);
        if (context.Error is not null)
        {
            return context.Error;
        }

        await ExpirePendingForSeatAsync(request.SeatId, ct);
        context = await sessions.PrepareCheckInPaymentAsync(user, request.SeatId, request.PricePlanId, request.CustomerId, ct, request.Items);
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

    public async Task<Result<CreatePaymentRequestResponse>> CreateAddOnAsync(
        CurrentUser user, Guid sessionId, AddOnSaleRequest request, CancellationToken ct)
    {
        var service = addOns ?? new AddOnService(db, clock);
        var context = await service.PreparePaymentAsync(user, sessionId, request, ct);
        if (context.Error is not null)
        {
            return context.Error;
        }

        await ExpirePendingForSeatAsync(context.Value!.SeatId, ct);
        context = await service.PreparePaymentAsync(user, sessionId, request, ct);
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

        var expired = ExpireIfOverdue(paymentRequest);
        await db.SaveChangesAsync(ct);
        if (expired)
        {
            await LogExpiredAsync(paymentRequest, "Hết hạn khi POS kiểm tra trạng thái.");
        }

        await LogPollAsync(paymentRequest, ct);
        var (points, customerName) = await PointsForPaymentAsync(paymentRequest, ct);
        return new PaymentRequestStatusResponse(
            paymentRequest.Code,
            paymentRequest.Amount,
            paymentRequest.Status,
            paymentRequest.ExpiresAt,
            paymentRequest.PaidAt,
            paymentRequest.Bill?.Code,
            points,
            customerName);
    }

    /// <summary>Points written by this payment's settlement (same bill, same timestamp as <c>PaidAt</c>).</summary>
    private async Task<(int Points, string? CustomerName)> PointsForPaymentAsync(PaymentRequest paymentRequest, CancellationToken ct)
    {
        if (paymentRequest.Status != PaymentRequestStatus.Paid || paymentRequest.BillId is not { } billId || paymentRequest.PaidAt is not { } paidAt)
        {
            return (0, null);
        }

        var rows = await db.PointTransactions.AsNoTracking()
            .Where(p => p.BillId == billId && p.CreatedAt == paidAt)
            .Select(p => new { p.Points, p.Customer.User.FullName })
            .ToListAsync(ct);
        return rows.Count == 0 ? (0, null) : (rows.Sum(r => r.Points), rows[0].FullName);
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
        await paymentLog.WriteAsync(new PaymentLogEntry(PaymentLogStage.Cancelled, "Nhân viên hủy yêu cầu thanh toán.")
        {
            Request = paymentRequest,
            Detail = new { cancelledBy = user.UserId },
        });
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
            foreach (var paymentRequest in expiredRequests)
            {
                await LogExpiredAsync(paymentRequest, "Hết hạn (tác vụ nền) mà chưa nhận được thanh toán.");
            }
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
            Items = context.Items,
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

        await paymentLog.WriteAsync(new PaymentLogEntry(PaymentLogStage.RequestCreated,
            paymentRequest.Kind switch
            {
                PaymentRequestKind.CheckIn => "Tạo mã QR thanh toán check-in.",
                PaymentRequestKind.AddOn => "Tạo mã QR thanh toán đồ thêm.",
                _ => "Tạo mã QR thanh toán gia hạn.",
            })
        {
            Request = paymentRequest,
            Detail = new
            {
                kind = paymentRequest.Kind.ToString(),
                paymentRequest.SeatId,
                paymentRequest.SessionId,
                paymentRequest.ShiftId,
                createdBy = user.UserId,
                paymentRequest.ExpiresAt,
                items = AddOnService.TryReadSnapshot(paymentRequest.Items, out var lines) && lines.Count > 0
                    ? lines.Select(l => new { l.Name, l.Quantity, l.UnitPrice, l.Amount })
                    : null,
            },
        });

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
        foreach (var paymentRequest in expired)
        {
            await LogExpiredAsync(paymentRequest, "Hết hạn khi tạo yêu cầu mới cho cùng chỗ ngồi.");
        }
    }

    private Task LogExpiredAsync(PaymentRequest paymentRequest, string message) =>
        paymentLog.WriteAsync(new PaymentLogEntry(PaymentLogStage.Expired, message)
        {
            Request = paymentRequest,
            Detail = new { paymentRequest.ExpiresAt },
        });

    /// <summary>Logs the first poll, every status change, and otherwise one poll per <see cref="PollLogInterval"/>.</summary>
    private async Task LogPollAsync(PaymentRequest paymentRequest, CancellationToken ct)
    {
        var status = paymentRequest.Status.ToString();
        DateTimeOffset? lastAt = null;
        string? lastDetail = null;
        try
        {
            var last = await db.PaymentLogs.AsNoTracking()
                .Where(l => l.PaymentRequestId == paymentRequest.Id && l.Stage == PaymentLogStage.StatusPolled)
                .OrderByDescending(l => l.OccurredAt)
                .Select(l => new { l.OccurredAt, l.Detail })
                .FirstOrDefaultAsync(ct);
            lastAt = last?.OccurredAt;
            lastDetail = last?.Detail;
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // Tracing must never break the status poll (e.g. PaymentLogs migration not applied yet).
            return;
        }

        var changed = lastAt is null || lastDetail is null || !lastDetail.Contains($"\"status\":\"{status}\"", StringComparison.Ordinal);
        if (!changed && clock.GetUtcNow() - lastAt!.Value < PollLogInterval)
        {
            return;
        }

        await paymentLog.WriteAsync(new PaymentLogEntry(PaymentLogStage.StatusPolled, $"POS kiểm tra trạng thái: {status}.")
        {
            Request = paymentRequest,
            BillCode = paymentRequest.Bill?.Code,
            Detail = new { status, billCode = paymentRequest.Bill?.Code },
        });
    }

    private bool ExpireIfOverdue(PaymentRequest paymentRequest)
    {
        if (paymentRequest.Status == PaymentRequestStatus.Pending && paymentRequest.ExpiresAt <= clock.GetUtcNow())
        {
            paymentRequest.Status = PaymentRequestStatus.Expired;
            return true;
        }

        return false;
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
    Guid? CustomerId,
    [property: System.ComponentModel.DataAnnotations.MaxLength(AddOnService.MaxLines), Description("Đồ thêm mua cùng lúc check-in (tùy chọn). Số tiền QR = giá gói + đồ thêm do máy chủ tính; danh sách món và đơn giá được lưu lại lúc tạo QR và dùng khi SePay xác nhận (đổi giá sau đó không ảnh hưởng).")] IReadOnlyList<AddOnLineRequest>? Items = null);

public sealed record CreatePaymentRequestResponse(string Code, decimal Amount, string QrUrl, DateTimeOffset ExpiresAt);

public sealed record PaymentRequestStatusResponse(
    string Code,
    decimal Amount,
    PaymentRequestStatus Status,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? PaidAt,
    string? BillCode,
    [property: Description("Điểm thành viên được cộng cho lần thanh toán này (0 nếu khách lẻ hoặc chưa thanh toán).")] int PointsAwarded = 0,
    [property: Description("Tên thành viên được cộng điểm, null nếu khách lẻ.")] string? CustomerName = null);
