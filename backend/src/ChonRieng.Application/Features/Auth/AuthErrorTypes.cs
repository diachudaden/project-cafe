namespace ChonRieng.Application.Features.Auth;

public static class AuthErrorTypes
{
    public const string Validation = "validation";
    public const string InvalidCredentials = "auth.invalid_credentials";
    public const string AccountLocked = "auth.account_locked";
    public const string ForbiddenClient = "auth.forbidden_client";
    public const string InvalidToken = "auth.invalid_token";
    public const string SessionRevoked = "auth.session_revoked";
}

public static class AuthErrorDetails
{
    public const string Validation = "Kiểm tra lại thông tin đăng nhập.";
    public const string InvalidCredentials = "Email/số điện thoại hoặc mật khẩu không đúng.";
    public const string AccountLocked = "Tài khoản đang bị khóa tạm thời. Thử lại sau.";
    public const string ForbiddenClient = "Tài khoản này không dùng được trên ứng dụng này.";
    public const string SessionRevoked = "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.";
}
