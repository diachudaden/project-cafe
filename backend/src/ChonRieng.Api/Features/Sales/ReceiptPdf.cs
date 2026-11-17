using System.Globalization;
using System.Reflection;
using ChonRieng.Api.Features.Common;
using QRCoder;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ChonRieng.Api.Features.Sales;

/// <summary>
/// 80 mm thermal-roll receipt rendered with QuestPDF (Community licence). Text uses the bundled Noto Sans (OFL,
/// embedded in the assembly so the Docker image always has it); system fonts are disabled so a missing glyph can
/// never silently fall back to a font without Vietnamese diacritics.
/// </summary>
public static class ReceiptPdf
{
    /// <summary>Family name stored in the bundled font files (QuestPDF 2026.9 resolves fonts by their own family name).</summary>
    public const string FontFamily = "Noto Sans";
    private const float PaperWidthMm = 80;
    private static readonly Lock InitLock = new();
    private static bool _initialized;

    /// <summary>Licence + font registration; idempotent, called at startup and before every render.</summary>
    public static void Initialize()
    {
        lock (InitLock)
        {
            if (_initialized)
            {
                return;
            }

            QuestPDF.Settings.License = LicenseType.Community;
            QuestPDF.Settings.UseSystemFonts = false;
            var assembly = typeof(ReceiptPdf).Assembly;
            foreach (var file in new[] { "NotoSans-Regular.ttf", "NotoSans-Bold.ttf" })
            {
                using var stream = assembly.GetManifestResourceStream($"ChonRieng.Api.Fonts.{file}")
                    ?? throw new InvalidOperationException($"Receipt font {file} is not embedded in {assembly.GetName().Name}.");
                FontManager.RegisterFontFromStream(stream);
            }

            _initialized = true;
        }
    }

    public static byte[] Render(ReceiptData bill, string? printedBy, string? claimUrl, DateTimeOffset printedAt)
    {
        Initialize();
        return Document.Create(doc => doc.Page(page =>
        {
            page.ContinuousSize(PaperWidthMm, Unit.Millimetre);
            page.Margin(4, Unit.Millimetre);
            page.DefaultTextStyle(t => t.FontFamily(FontFamily).FontSize(8).LineHeight(1.25f));
            page.Content().Column(col =>
            {
                col.Spacing(2);
                col.Item().AlignCenter().Text(bill.BranchName).Bold().FontSize(11);
                col.Item().AlignCenter().Text(bill.BranchAddress).FontSize(7);
                Divider(col);

                col.Item().AlignCenter().Text("HÓA ĐƠN").Bold().FontSize(10);
                col.Item().AlignCenter().Text(bill.Code).Bold().FontSize(14).LetterSpacing(0.1f);
                Pair(col, "Chỗ ngồi", bill.SeatCode);
                Pair(col, "Gói", bill.PlanName);
                Pair(col, "Bắt đầu", Time(bill.StartAt));
                Pair(col, "Kết thúc", Time(bill.EndAt));
                Divider(col);

                Pair(col, $"Lượt ngồi · {bill.PlanName}", Money(bill.PlanAmount));
                foreach (var e in bill.Extensions)
                {
                    Pair(col, $"Gia hạn +{e.AddedMinutes} phút ({e.CreatedAt.ToVietnam():HH:mm})", Money(e.Price));
                }

                if (bill.Items.Count > 0)
                {
                    col.Item().PaddingTop(2).Text("Đồ thêm").Bold();
                    foreach (var i in bill.Items)
                    {
                        Pair(col, $"{i.Name} × {i.Quantity} ({Money(i.UnitPrice)})", Money(i.Amount));
                    }
                }

                Divider(col);
                if (bill.CashPaidTotal > 0)
                {
                    Pair(col, "Tiền mặt", Money(bill.CashPaidTotal));
                }

                if (bill.VietQrPaidTotal > 0)
                {
                    Pair(col, "VietQR", Money(bill.VietQrPaidTotal));
                }

                col.Item().Row(row =>
                {
                    row.RelativeItem().Text("TỔNG CỘNG").Bold().FontSize(10);
                    row.AutoItem().Text(Money(bill.Total)).Bold().FontSize(10);
                });
                Divider(col);

                if (bill.Claimed)
                {
                    Pair(col, "Thành viên", bill.MemberName!);
                    Pair(col, "Điểm được cộng", $"+{bill.PointsEarned.ToString("#,0", CultureInfo.InvariantCulture).Replace(',', '.')} điểm");
                }
                else if (claimUrl is not null)
                {
                    col.Item().PaddingTop(2).AlignCenter().Width(32, Unit.Millimetre).Image(Qr(claimUrl));
                    col.Item().AlignCenter().Text("Quét để tích điểm").Bold();
                    col.Item().AlignCenter().Text($"hoặc nhập mã {bill.Code} trong {Loyalty.ClaimWindowDays} ngày").FontSize(7);
                }

                Divider(col);
                if (!string.IsNullOrWhiteSpace(printedBy))
                {
                    Pair(col, "Nhân viên", printedBy);
                }

                Pair(col, "In lúc", Time(printedAt));
                col.Item().PaddingTop(2).AlignCenter().Text("Cảm ơn quý khách!").Italic();
            });
        })).GeneratePdf();
    }

    /// <summary>"45.000đ", independent of the server culture (the container may run with invariant globalization).</summary>
    public static string Money(decimal amount) =>
        amount.ToString("#,0", CultureInfo.InvariantCulture).Replace(',', '.') + "đ";

    private static string Time(DateTimeOffset value) => value.ToVietnam().ToString("HH:mm dd/MM/yyyy", CultureInfo.InvariantCulture);

    private static void Pair(ColumnDescriptor col, string label, string value) =>
        col.Item().Row(row =>
        {
            row.RelativeItem().Text(label);
            row.AutoItem().PaddingLeft(4).Text(value);
        });

    private static void Divider(ColumnDescriptor col) =>
        col.Item().PaddingVertical(2).LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);

    private static byte[] Qr(string text)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
        return new PngByteQRCode(data).GetGraphic(8);
    }
}
