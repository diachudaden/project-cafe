using System.ComponentModel;
using System.Security.Claims;
using ChonRieng.Api.Features.Common;
using ChonRieng.Domain.Entities;
using ChonRieng.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChonRieng.Api.Features.Sales;

public sealed record PaymentLogView(
    Guid Id,
    DateTimeOffset OccurredAt,
    PaymentLogStage Stage,
    PaymentLogLevel Level,
    Guid? PaymentRequestId,
    string? PaymentCode,
    string? ProviderTxId,
    string? BillCode,
    Guid? BranchId,
    decimal? Amount,
    string Message,
    [property: Description("JSON nhỏ mô tả chi tiết bước; không chứa secret hay nội dung chuyển khoản.")] string? Detail,
    string? TraceId,
    [property: Description("Số mili giây kể từ lúc tạo mã QR.")] long? ElapsedMs);

public sealed record PaymentLogPage(int Page, int PageSize, int Total, IReadOnlyList<PaymentLogView> Items);

public sealed record PaymentTraceRequest(
    Guid Id,
    string Code,
    PaymentRequestKind Kind,
    PaymentRequestStatus Status,
    decimal Amount,
    Guid BranchId,
    string BranchName,
    string SeatCode,
    string? CreatedByName,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? PaidAt,
    string? BillCode);

public sealed record PaymentTraceTransaction(
    Guid Id,
    string ProviderTxId,
    long Amount,
    string? Code,
    [property: Description("Nội dung chuyển khoản; chỉ Admin thấy.")] string? Content,
    string? ReferenceCode,
    DateTimeOffset OccurredAt,
    DateTimeOffset ReceivedAt,
    PaymentMatchStatus MatchStatus,
    string? Note,
    [property: Description("true: giao dịch không khớp mã nhưng cùng số tiền và thời gian, có thể là của yêu cầu này.")] bool Suspect);

public sealed record PaymentTrace(
    PaymentTraceRequest Request,
    IReadOnlyList<PaymentTraceTransaction> Transactions,
    IReadOnlyList<PaymentLogView> Logs,
    [property: Description("Các log không gắn mã (webhook bị từ chối...) trong khoảng thời gian của yêu cầu.")] IReadOnlyList<PaymentLogView> RelatedLogs,
    [property: Description("Nhận định tự động: luồng dừng ở đâu.")] IReadOnlyList<string> Diagnosis);

public sealed class PaymentLogQueryService(AppDbContext db, TimeProvider clock)
{
    /// <summary>After a request expires, a late webhook may still arrive; look this far past expiry.</summary>
    private static readonly TimeSpan LateWindow = TimeSpan.FromMinutes(30);

    public async Task<PaymentLogPage> ListAsync(
        CurrentUser user,
        Guid? branchId,
        string? search,
        string? stage,
        string? level,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 500);
        var query = Scoped(user);
        if (branchId is { } b)
        {
            query = query.Where(l => l.BranchId == b);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var upper = term.ToUpperInvariant();
            query = query.Where(l => (l.PaymentCode != null && l.PaymentCode.Contains(upper))
                || (l.BillCode != null && l.BillCode.Contains(upper))
                || (l.ProviderTxId != null && l.ProviderTxId.Contains(term))
                || l.Message.Contains(term));
        }

        var stages = ParseEnums<PaymentLogStage>(stage);
        if (stages.Count > 0)
        {
            query = query.Where(l => stages.Contains(l.Stage));
        }

        var levels = ParseEnums<PaymentLogLevel>(level);
        if (levels.Count > 0)
        {
            query = query.Where(l => levels.Contains(l.Level));
        }

        if (from is { } f)
        {
            query = query.Where(l => l.OccurredAt >= f);
        }

        if (to is { } t)
        {
            query = query.Where(l => l.OccurredAt <= t);
        }

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(l => l.OccurredAt).ThenByDescending(l => l.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(ToView)
            .ToListAsync(ct);
        return new PaymentLogPage(page, pageSize, total, items);
    }

    public async Task<Result<PaymentTrace>> TraceAsync(CurrentUser user, string code, CancellationToken ct)
    {
        var normalized = code.Trim().ToUpperInvariant();
        var request = await db.PaymentRequests.AsNoTracking()
            .Where(r => r.Code == normalized)
            .Select(r => new PaymentTraceRequest(r.Id, r.Code, r.Kind, r.Status, r.Amount, r.BranchId, r.Branch.Name, r.Seat.Code,
                db.Users.Where(u => u.Id == r.CreatedByUserId).Select(u => u.FullName).FirstOrDefault(),
                r.CreatedAt, r.ExpiresAt, r.PaidAt, r.Bill != null ? r.Bill.Code : null))
            .SingleOrDefaultAsync(ct);
        if (request is null || !user.CanAccessBranch(request.BranchId) || !(user.IsAdmin || user.IsManager))
        {
            return ServiceError.NotFound("Không tìm thấy yêu cầu thanh toán.");
        }

        var logs = await db.PaymentLogs.AsNoTracking()
            .Where(l => l.PaymentRequestId == request.Id || l.PaymentCode == request.Code)
            .OrderBy(l => l.OccurredAt).ThenBy(l => l.Id)
            .Select(ToView)
            .ToListAsync(ct);

        var windowEnd = request.ExpiresAt + LateWindow;
        // Callbacks that could not be tied to any code (rejected, unmatched...) during this request's lifetime.
        var related = await db.PaymentLogs.AsNoTracking()
            .Where(l => l.PaymentRequestId == null && l.OccurredAt >= request.CreatedAt && l.OccurredAt <= windowEnd
                && l.Stage != PaymentLogStage.WebhookReceived
                && (l.PaymentCode == null || l.PaymentCode != request.Code))
            .OrderBy(l => l.OccurredAt)
            .Take(100)
            .Select(ToView)
            .ToListAsync(ct);
        var webhooksInWindow = await db.PaymentLogs.AsNoTracking()
            .CountAsync(l => l.Stage == PaymentLogStage.WebhookReceived && l.OccurredAt >= request.CreatedAt && l.OccurredAt <= windowEnd, ct);

        var showContent = user.IsAdmin;
        var transactions = await db.PaymentTransactions.AsNoTracking()
            .Where(t => t.PaymentRequestId == request.Id
                || (t.PaymentRequestId == null && t.Amount == (long)request.Amount
                    && t.ReceivedAt >= request.CreatedAt && t.ReceivedAt <= windowEnd))
            .OrderBy(t => t.ReceivedAt)
            .Select(t => new PaymentTraceTransaction(t.Id, t.ProviderTxId, t.Amount, t.Code, showContent ? t.Content : null, t.ReferenceCode,
                t.OccurredAt, t.ReceivedAt, t.MatchStatus, t.Note, t.PaymentRequestId == null))
            .ToListAsync(ct);

        return new PaymentTrace(request, transactions, logs, related, Diagnose(request, logs, related, transactions, webhooksInWindow));
    }

    private List<string> Diagnose(
        PaymentTraceRequest request,
        IReadOnlyList<PaymentLogView> logs,
        IReadOnlyList<PaymentLogView> related,
        IReadOnlyList<PaymentTraceTransaction> transactions,
        int webhooksInWindow)
    {
        var now = clock.GetUtcNow();
        var notes = new List<string>();
        var polls = logs.Where(l => l.Stage == PaymentLogStage.StatusPolled).ToList();
        var lastPoll = polls.LastOrDefault();
        var settled = logs.LastOrDefault(l => l.Stage == PaymentLogStage.Settled);
        var matched = transactions.Where(t => !t.Suspect).ToList();
        var suspects = transactions.Where(t => t.Suspect).ToList();
        var rejected = related.Where(l => l.Stage is PaymentLogStage.WebhookRejected or PaymentLogStage.Exception).ToList();

        switch (request.Status)
        {
            case PaymentRequestStatus.Paid:
                notes.Add(settled?.ElapsedMs is { } ms
                    ? $"Hoàn tất: bill {request.BillCode} được tạo sau {ms / 1000.0:0.#} giây kể từ khi tạo mã QR."
                    : $"Hoàn tất: bill {request.BillCode}.");
                if (matched.FirstOrDefault() is { } paidTx)
                {
                    var bankToServer = (paidTx.ReceivedAt - paidTx.OccurredAt).TotalSeconds;
                    if (bankToServer > 60)
                    {
                        notes.Add($"SePay báo về chậm {bankToServer:0} giây sau khi ngân hàng ghi nhận giao dịch: độ trễ nằm ở phía SePay/ngân hàng.");
                    }
                }

                if (settled is not null && !polls.Any(p => p.OccurredAt >= settled.OccurredAt))
                {
                    notes.Add(lastPoll is null
                        ? "POS chưa từng kiểm tra trạng thái của mã này: màn hình QR có thể đã bị đóng hoặc mất mạng."
                        : $"POS ngừng kiểm tra trạng thái lúc {VietnamTime.ToVietnam(lastPoll.OccurredAt):HH:mm:ss}, trước khi bill được tạo: màn hình không tự chuyển sang thành công. Mở lại bill bằng mã {request.BillCode}.");
                }
                else if (settled is not null)
                {
                    var seen = polls.First(p => p.OccurredAt >= settled.OccurredAt);
                    notes.Add($"POS nhận trạng thái Paid sau {(seen.OccurredAt - settled.OccurredAt).TotalSeconds:0.#} giây.");
                }

                break;

            case PaymentRequestStatus.NeedsAttention:
                var problem = logs.LastOrDefault(l => l.Level != PaymentLogLevel.Info);
                notes.Add($"Cần xử lý thủ công: {problem?.Message ?? "đã nhận giao dịch nhưng không tự hoàn tất."}");
                break;

            default:
                if (matched.Count == 0)
                {
                    if (rejected.Count > 0)
                    {
                        notes.Add($"Có {rejected.Count} webhook bị từ chối/lỗi trong thời gian của mã này (vd: {rejected[0].Message}). Nhiều khả năng tiền của khách nằm ở đây: sửa cấu hình rồi để SePay gửi lại.");
                    }

                    if (suspects.Count > 0)
                    {
                        notes.Add($"Có {suspects.Count} giao dịch cùng số tiền nhưng không khớp mã (vd: {suspects[0].ProviderTxId}, {suspects[0].Note}). Khách có thể đã sửa/xóa nội dung chuyển khoản.");
                    }

                    if (webhooksInWindow == 0)
                    {
                        notes.Add(request.ExpiresAt <= now
                            ? "Không nhận được webhook nào từ SePay trong suốt thời gian của mã: kiểm tra URL webhook trên SePay, HTTPS và nginx."
                            : "Chưa nhận được webhook nào từ SePay kể từ khi tạo mã.");
                    }
                    else if (rejected.Count == 0 && suspects.Count == 0)
                    {
                        notes.Add($"Đã nhận {webhooksInWindow} webhook trong thời gian này nhưng không cái nào thuộc mã {request.Code}.");
                    }
                }

                if (request.Status == PaymentRequestStatus.Pending && request.ExpiresAt > now
                    && (lastPoll is null || now - lastPoll.OccurredAt > TimeSpan.FromSeconds(75)))
                {
                    notes.Add("POS không còn kiểm tra trạng thái (không có lượt kiểm tra trong hơn 1 phút).");
                }

                break;
        }

        return notes;
    }

    private IQueryable<PaymentLog> Scoped(CurrentUser user)
    {
        var logs = db.PaymentLogs.AsNoTracking();
        if (user.IsAdmin)
        {
            return logs;
        }

        // Managers see their branches; callbacks without a branch (rejected/unmatched) are Admin-only.
        var scope = user.Scope.BranchIds.ToList();
        return logs.Where(l => l.BranchId != null && scope.Contains(l.BranchId.Value));
    }

    private static List<T> ParseEnums<T>(string? value) where T : struct, Enum =>
        (value ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(v => Enum.TryParse<T>(v, true, out var parsed) ? parsed : (T?)null)
            .Where(v => v is not null).Select(v => v!.Value).ToList();

    private static readonly System.Linq.Expressions.Expression<Func<PaymentLog, PaymentLogView>> ToView = l =>
        new PaymentLogView(l.Id, l.OccurredAt, l.Stage, l.Level, l.PaymentRequestId, l.PaymentCode, l.ProviderTxId, l.BillCode,
            l.BranchId, l.Amount, l.Message, l.Detail, l.TraceId, l.ElapsedMs);
}

public static class PaymentLogEndpoints
{
    public static void MapPaymentLogEndpoints(this WebApplication app)
    {
        var logs = app.MapGroup("/api/payment-logs").WithTags("Nhật ký thanh toán").RequireManagement();

        logs.MapGet("/", async (
                Guid? branchId, string? search, string? stage, string? level, DateTimeOffset? from, DateTimeOffset? to, int? page, int? pageSize,
                ClaimsPrincipal user, PaymentLogQueryService service, CancellationToken ct) =>
                TypedResults.Ok(await service.ListAsync(CurrentUser.From(user), branchId, search, stage, level, from, to, page ?? 1, pageSize ?? 50, ct)))
            .WithName("ListPaymentLogs")
            .WithSummary("Nhật ký các bước thanh toán VietQR")
            .WithDescription("Lọc theo chi nhánh, từ khóa (mã thanh toán, mã bill, mã giao dịch SePay, nội dung), stage, level (phân tách bằng dấu phẩy) và khoảng thời gian. Admin thấy cả webhook không gắn chi nhánh; quản lý chỉ thấy chi nhánh mình.");

        logs.MapGet("/{code}", async (string code, ClaimsPrincipal user, PaymentLogQueryService service, CancellationToken ct) =>
                (await service.TraceAsync(CurrentUser.From(user), code, ct)).ToHttp())
            .WithName("GetPaymentTrace")
            .WithSummary("Dòng thời gian của một mã thanh toán")
            .WithDescription("Trả về yêu cầu thanh toán, giao dịch ngân hàng liên quan (kể cả giao dịch nghi ngờ cùng số tiền), toàn bộ log và nhận định luồng bị dừng ở đâu.");
    }
}
