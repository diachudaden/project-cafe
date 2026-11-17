using System.ComponentModel.DataAnnotations;
using ChonRieng.Api.Features.Common;
using ChonRieng.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChonRieng.Api.Features.Sales;

/// <summary>Optional settings for printed receipts (<c>Receipt__CustomerAppUrl</c>).</summary>
public sealed class ReceiptOptions
{
    public const string SectionName = "Receipt";

    /// <summary>Origin of the customer app used in the claim QR; when empty the API request's origin is used.</summary>
    [Url]
    public string? CustomerAppUrl { get; set; }
}

/// <summary>Everything printed on an 80 mm receipt; amounts and lines exactly as stored on the bill.</summary>
public sealed record ReceiptData(
    string Code,
    string BranchName,
    string BranchAddress,
    string SeatCode,
    string PlanName,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    IReadOnlyList<MyBillExtension> Extensions,
    IReadOnlyList<BillItemView> Items,
    decimal CashPaidTotal,
    decimal VietQrPaidTotal,
    decimal Total,
    DateTimeOffset CreatedAt,
    string? MemberName,
    int PointsEarned)
{
    /// <summary>The check-in (plan) amount: the bill total minus extensions and add-on lines.</summary>
    public decimal PlanAmount => Total - Extensions.Sum(e => e.Price) - Items.Sum(i => i.Amount);

    public bool Claimed => MemberName is not null;
}

public sealed record ReceiptFile(byte[] Content, string FileName);

/// <summary>Printable bill (PDF) for the POS; the branch scope is applied in the query.</summary>
public sealed class ReceiptService(AppDbContext db, TimeProvider clock)
{
    public async Task<Result<ReceiptFile>> RenderAsync(CurrentUser user, string code, string customerAppUrl, CancellationToken ct)
    {
        var data = await LoadAsync(user, code, ct);
        if (data is null)
        {
            return ServiceError.NotFound("Không tìm thấy hóa đơn.");
        }

        var printedBy = await db.Users.AsNoTracking().Where(u => u.Id == user.UserId).Select(u => u.FullName).SingleOrDefaultAsync(ct);
        var claimUrl = data.Claimed ? null : ClaimUrl(customerAppUrl, data.Code);
        var pdf = ReceiptPdf.Render(data, printedBy, claimUrl, clock.GetUtcNow());
        return new ReceiptFile(pdf, $"bill-{data.Code}.pdf");
    }

    /// <summary>The bill if it belongs to a branch the user works at (Admin: any); null otherwise.</summary>
    public async Task<ReceiptData?> LoadAsync(CurrentUser user, string code, CancellationToken ct)
    {
        if (!user.IsAdmin && !user.IsManager && !user.IsStaff)
        {
            return null;
        }

        var normalized = Loyalty.NormalizeCode(code);
        var global = user.IsAdmin;
        var branchIds = user.Scope.BranchIds.ToList();
        return await db.Bills.AsNoTracking()
            .Where(b => b.Code == normalized && (global || branchIds.Contains(b.BranchId)))
            .Select(b => new ReceiptData(
                b.Code,
                b.Branch.Name,
                b.Branch.Address,
                b.Session.Seat.Code,
                b.Session.PricePlan.Name,
                b.Session.StartAt,
                b.Session.EndAt,
                b.Session.Extensions.OrderBy(e => e.CreatedAt).Select(e => new MyBillExtension(e.CreatedAt, e.AddedMinutes, e.Price)).ToList(),
                b.Items.OrderBy(i => i.CreatedAt)
                    .Select(i => new BillItemView(i.Id, i.Name, i.UnitPrice, i.Quantity, i.Amount, i.PaymentMethod, i.CreatedAt)).ToList(),
                b.CashPaidTotal,
                b.VietQrPaidTotal,
                b.Total,
                b.CreatedAt,
                b.ClaimedByCustomer != null ? b.ClaimedByCustomer.User.FullName : null,
                b.PointTransactions.Sum(p => p.Points)))
            .SingleOrDefaultAsync(ct);
    }

    /// <summary>Customer claim page with the bill code prefilled.</summary>
    public static string ClaimUrl(string customerAppUrl, string code) =>
        $"{customerAppUrl.TrimEnd('/')}/claim?code={Uri.EscapeDataString(code)}";
}
