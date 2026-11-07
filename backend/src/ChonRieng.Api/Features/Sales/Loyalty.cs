using System.Security.Cryptography;
using ChonRieng.Domain.Entities;

namespace ChonRieng.Api.Features.Sales;

/// <summary>Points and tier rules (decision D8): 1.000đ = 1 point; tier by total spent.</summary>
public static class Loyalty
{
    public const string Bronze = "Bronze";
    public const string Silver = "Silver";
    public const string Gold = "Gold";
    public const decimal SilverThreshold = 500_000m;
    public const decimal GoldThreshold = 2_000_000m;
    public const int ClaimWindowDays = 7;

    // No 0/O/1/I so codes printed on a bill are easy to type.
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static int PointsFor(decimal amount) => amount <= 0 ? 0 : (int)Math.Floor(amount / 1000m);

    public static string TierFor(decimal totalSpent) =>
        totalSpent >= GoldThreshold ? Gold : totalSpent >= SilverThreshold ? Silver : Bronze;

    public static string NewBillCode() => RandomNumberGenerator.GetString(CodeAlphabet, 8);

    public static string NewMemberCode() => "CR" + RandomNumberGenerator.GetString(CodeAlphabet, 8);

    public static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();

    /// <summary>Adds points and spending for <paramref name="amount"/> of <paramref name="bill"/> to the customer.</summary>
    public static PointTransaction Award(Customer customer, Bill bill, decimal amount, DateTimeOffset now, string reason)
    {
        var points = PointsFor(amount);
        customer.Points += points;
        customer.TotalSpent += amount;
        customer.Tier = TierFor(customer.TotalSpent);
        return new PointTransaction
        {
            CustomerId = customer.Id,
            BillId = bill.Id,
            Points = points,
            Reason = reason,
            CreatedAt = now,
        };
    }
}
