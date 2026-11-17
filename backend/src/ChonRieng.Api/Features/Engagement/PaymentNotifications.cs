using System.Globalization;
using ChonRieng.Domain.Entities;
using ChonRieng.Infrastructure.Persistence;

namespace ChonRieng.Api.Features.Engagement;

public enum PaymentKind
{
    CheckIn,
    Extension,
    AddOn,
}

/// <summary>
/// The member-facing notification for every completed payment and bill claim (TASK-025). Call it right after
/// <c>Loyalty.Award</c>, before the payment's <c>SaveChanges</c>, so it is saved (or rolled back) with the payment.
/// Only call it for a bill with a member (<c>Bill.ClaimedByCustomerId</c>): walk-ins are never notified.
/// </summary>
public static class PaymentNotifications
{
    public static string BillLink(string billCode) => $"/bills/{billCode}";

    /// <param name="customer">Tracked member whose <c>Points</c> already include this payment.</param>
    public static void PaymentCompleted(
        this AppDbContext db, Customer customer, Bill bill, string seatCode, PaymentKind kind,
        decimal amount, PaymentMethod method, int pointsEarned, DateTimeOffset now)
    {
        var (title, what) = kind switch
        {
            PaymentKind.CheckIn => ($"Thanh toán check-in thành công · {seatCode}", "check-in"),
            PaymentKind.Extension => ($"Gia hạn thành công · {seatCode}", "gia hạn"),
            _ => ($"Thanh toán đồ thêm thành công · {seatCode}", "đồ thêm"),
        };
        db.Notify(customer.UserId, Notifier.PaymentReceived, title,
            $"Đã nhận {Money(amount)} ({MethodLabel(method)}) cho {what} chỗ {seatCode}, bill {bill.Code}. {PointsText(pointsEarned, customer.Points)}",
            BillLink(bill.Code), now);
    }

    public static void PointsClaimed(this AppDbContext db, Customer customer, Bill bill, string? seatCode, int pointsEarned, DateTimeOffset now)
    {
        var where = seatCode is null ? "" : $" (chỗ {seatCode})";
        db.Notify(customer.UserId, Notifier.PointsEarned, $"Tích điểm thành công · bill {bill.Code}",
            $"Bill {bill.Code}{where} trị giá {Money(bill.Total)}. {PointsText(pointsEarned, customer.Points)}",
            BillLink(bill.Code), now);
    }

    public static string Money(decimal amount) => $"{Number(amount)}đ";

    /// <summary>Vietnamese grouping ("1.250.000") without depending on the server's culture data.</summary>
    public static string Number(decimal value) =>
        decimal.Round(value).ToString("#,0", CultureInfo.InvariantCulture).Replace(',', '.');

    private static string PointsText(int earned, int balance) =>
        $"+{Number(earned)} điểm · số dư {Number(balance)} điểm.";

    private static string MethodLabel(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "Tiền mặt",
        PaymentMethod.VietQr => "VietQR",
        _ => method.ToString(),
    };
}
