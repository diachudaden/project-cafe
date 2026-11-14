using System.Text;
using ChonRieng.Api.Features.Sales;
using ChonRieng.Domain.Entities;
using UglyToad.PdfPig;
using Xunit;

namespace ChonRieng.UnitTests.Features;

/// <summary>TASK-026: the 80 mm receipt renders Vietnamese with the bundled font and shows every bill line.</summary>
public sealed class ReceiptPdfTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 14, 2, 0, 0, TimeSpan.Zero); // 09:00 VN

    private static ReceiptData Bill(string? member = null, int points = 0) => new(
        "QW12ER34", "Chốn Riêng – Chi nhánh Đống Đa", "12 Đường Láng, Hà Nội", "A-01", "Gói 4 giờ",
        Start, Start.AddHours(6),
        [new MyBillExtension(Start.AddHours(4), 120, 40_000)],
        [
            new BillItemView(Guid.NewGuid(), "Mì ly trứng", 15_000, 2, 30_000, PaymentMethod.Cash, Start),
            new BillItemView(Guid.NewGuid(), "Trà đá", 5_000, 1, 5_000, PaymentMethod.VietQr, Start.AddHours(1)),
        ],
        CashPaidTotal: 115_000, VietQrPaidTotal: 5_000, Total: 120_000, CreatedAt: Start, member, points);

    [Fact]
    public void Unclaimed_receipt_has_all_lines_in_vietnamese_and_the_claim_qr()
    {
        var pdf = ReceiptPdf.Render(Bill(), "Nguyễn Thị Hương", "https://cafe.example/claim?code=QW12ER34", Start.AddHours(6));
        if (Environment.GetEnvironmentVariable("RECEIPT_SAMPLE_PATH") is { Length: > 0 } sample)
        {
            File.WriteAllBytes(sample, pdf); // manual visual check: RECEIPT_SAMPLE_PATH=/tmp/x.pdf dotnet test
        }

        var text = Text(pdf, out var fonts, out var widthPt);

        Assert.Contains("QW12ER34", text);
        foreach (var expected in new[]
                 {
                     "Chốn Riêng – Chi nhánh Đống Đa", "HÓA ĐƠN", "Chỗ ngồi A-01", "Gói 4 giờ", "Lượt ngồi · Gói 4 giờ 45.000đ", "Gia hạn +120 phút", "40.000đ",
                     "Đồ thêm", "Mì ly trứng × 2", "30.000đ", "Trà đá × 1", "Tiền mặt 115.000đ", "VietQR 5.000đ", "TỔNG CỘNG 120.000đ",
                     "Quét để tích điểm", "Nhân viên Nguyễn Thị Hương", "In lúc 15:00 14/11/2026", "Bắt đầu 09:00 14/11/2026",
                 })
        {
            Assert.Contains(Nfc(expected), text);
        }

        Assert.DoesNotContain("Thành viên", text);
        Assert.All(fonts, f => Assert.Contains("NotoSans", f));
        Assert.InRange(widthPt, 226, 228); // 80 mm = 226.77 pt
    }

    [Fact]
    public void Claimed_receipt_shows_member_and_points_instead_of_the_qr()
    {
        var text = Text(ReceiptPdf.Render(Bill("Trần Văn Đức", 120), null, null, Start), out _, out _);

        Assert.Contains(Nfc("Thành viên Trần Văn Đức"), text);
        Assert.Contains(Nfc("Điểm được cộng +120 điểm"), text);
        Assert.DoesNotContain(Nfc("Quét để tích điểm"), text);
    }

    [Fact]
    public void Plan_amount_is_the_total_minus_extensions_and_add_ons()
    {
        Assert.Equal(45_000, Bill().PlanAmount);
        Assert.Equal("https://cafe.example/claim?code=QW12ER34", ReceiptService.ClaimUrl("https://cafe.example/", "QW12ER34"));
    }

    private static string Text(byte[] pdf, out IReadOnlySet<string> fonts, out double widthPt)
    {
        Assert.True(pdf.Length > 1000);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
        using var document = PdfDocument.Open(pdf);
        var page = Assert.Single(document.GetPages());
        fonts = page.Letters.Select(l => l.FontName).ToHashSet();
        widthPt = page.Width;
        return Nfc(string.Join(" ", page.GetWords().Select(w => w.Text)));
    }

    private static string Nfc(string s) => s.Normalize(NormalizationForm.FormC);
}
