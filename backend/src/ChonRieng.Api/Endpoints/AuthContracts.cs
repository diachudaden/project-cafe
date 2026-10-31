using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace ChonRieng.Api.Endpoints;

/// <summary>Dữ liệu đăng ký tài khoản khách hàng.</summary>
public sealed record RegisterRequest
{
    [Required, EmailAddress, MaxLength(256), Description("Email dùng để đăng nhập.")]
    public required string Email { get; init; }

    [Required, MinLength(12), MaxLength(128), Description("Mật khẩu tối thiểu 12 ký tự, gồm chữ hoa, chữ thường, số và ký tự đặc biệt.")]
    public required string Password { get; init; }

    [Required, MaxLength(150), Description("Họ tên hiển thị của khách hàng.")]
    public required string FullName { get; init; }

    [Phone, MaxLength(32), Description("Số điện thoại tùy chọn.")]
    public string? PhoneNumber { get; init; }

    [MaxLength(200), Description("Tên thiết bị tùy chọn để nhận diện phiên đăng nhập.")]
    public string? DeviceId { get; init; }
}

/// <summary>Thông tin đăng nhập của tài khoản hiện có.</summary>
public sealed record LoginRequest
{
    [Required, EmailAddress, MaxLength(256), Description("Email đã đăng ký.")]
    public required string Email { get; init; }

    [Required, MaxLength(128), Description("Mật khẩu của tài khoản.")]
    public required string Password { get; init; }

    [MaxLength(200), Description("Tên thiết bị tùy chọn để nhận diện phiên đăng nhập.")]
    public string? DeviceId { get; init; }
}

/// <summary>Dữ liệu đổi refresh token lấy cặp token mới.</summary>
public sealed record RefreshTokenRequest
{
    [Required, Description("Refresh token hiện tại; token cũ sẽ bị thay thế sau khi làm mới thành công.")]
    public required string RefreshToken { get; init; }
}

/// <summary>Dữ liệu thu hồi refresh token.</summary>
public sealed record RevokeTokenRequest
{
    [Required, Description("Refresh token cần thu hồi.")]
    public required string RefreshToken { get; init; }
}

/// <summary>Thông tin tài khoản an toàn do API xác thực trả về.</summary>
public sealed record AuthUserResponse(
    [property: Description("Mã định danh tài khoản.")] Guid Id,
    [property: Description("Email đăng nhập.")] string Email,
    [property: Description("Họ tên hiển thị.")] string FullName,
    [property: Description("Số điện thoại nếu đã cung cấp.")] string? PhoneNumber,
    [property: Description("Các vai trò hiện được gán cho tài khoản.")] IReadOnlyList<string> Roles,
    [property: Description("Các chi nhánh được gán (BranchManager/Staff); rỗng với Admin và Customer.")] IReadOnlyList<Guid> BranchIds);

/// <summary>Access token và refresh token được cấp sau khi xác thực.</summary>
public sealed record AuthTokenResponse(
    [property: Description("JWT dùng trong header Authorization: Bearer.")] string AccessToken,
    [property: Description("Thời điểm access token hết hạn, bao gồm múi giờ.")] DateTimeOffset AccessTokenExpiresAt,
    [property: Description("Refresh token dùng một lần để lấy cặp token mới.")] string RefreshToken,
    [property: Description("Thời điểm refresh token hết hạn.")] DateTimeOffset RefreshTokenExpiresAt,
    [property: Description("Thông tin tài khoản an toàn; không chứa thông tin xác thực.")] AuthUserResponse User);

/// <summary>Kết quả tạo tài khoản, bao gồm lỗi xác thực dữ liệu an toàn nếu thất bại.</summary>
public sealed record RegistrationResult(
    AuthTokenResponse? Tokens,
    IReadOnlyDictionary<string, string[]> Errors);
