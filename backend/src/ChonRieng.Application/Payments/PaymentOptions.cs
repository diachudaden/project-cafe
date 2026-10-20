using System.ComponentModel.DataAnnotations;

namespace ChonRieng.Application.Payments;

/// <summary>Section <c>Payment</c> trong appsettings. Secret đọc từ user-secrets / biến môi trường.</summary>
public sealed class PaymentOptions
{
    public const string SectionName = "Payment";

    /// <summary>Tên ngân hàng theo danh sách VietQR (short_name/alias/code/bin), ví dụ "MBBank".</summary>
    [Required] public string Bank { get; set; } = "";

    /// <summary>Số tài khoản nhận tiền (tài khoản riêng cho demo).</summary>
    [Required] public string AccountNumber { get; set; } = "";

    /// <summary>Tên chủ tài khoản, viết hoa không dấu. Chỉ để hiển thị.</summary>
    public string AccountHolder { get; set; } = "";

    /// <summary>Tiền tố mã thanh toán, phải trùng cấu hình "Cấu trúc mã thanh toán" trên SePay.</summary>
    [Required, RegularExpression("^[A-Za-z]{2,5}$")] public string CodePrefix { get; set; } = "CR";

    /// <summary>Chuỗi bắt buộc đặt trước mã trong nội dung CK (ví dụ "SEVQR" với VietinBank cá nhân). Để trống nếu không cần.</summary>
    public string DescriptionPrefix { get; set; } = "";

    /// <summary>Secret HMAC-SHA256 của webhook SePay. KHÔNG commit.</summary>
    public string WebhookSecret { get; set; } = "";

    [Range(30, 3600)] public int TimestampToleranceSeconds { get; set; } = 300;

    [Range(1, 120)] public int SeatHoldMinutes { get; set; } = 10;

    /// <summary>Kiểu ảnh QR: "", "compact", "qronly", "standee".</summary>
    public string QrTemplate { get; set; } = "compact";

    /// <summary>Gốc URL tạo ảnh VietQR.</summary>
    public string QrImageBaseUrl { get; set; } = "https://vietqr.app/img";
}
