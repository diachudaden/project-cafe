using ChonRieng.Api.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace ChonRieng.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth")
            .WithTags("Xác thực");

        group.MapPost("/register", async Task<Results<Created<AuthTokenResponse>, ValidationProblem>> (
            RegisterRequest request,
            IAuthService authService,
            CancellationToken cancellationToken) =>
        {
            var result = await authService.RegisterAsync(request, cancellationToken);
            return result.Tokens is null
                ? TypedResults.ValidationProblem(result.Errors.ToDictionary())
                : TypedResults.Created("/api/auth/me", result.Tokens);
        })
            .AllowAnonymous()
            .RequireRateLimiting(AuthRateLimits.Register)
            .WithName("RegisterCustomer")
            .WithSummary("Đăng ký tài khoản khách hàng")
            .WithDescription("Tạo tài khoản khách hàng, hồ sơ khách hàng và trả về access token cùng refresh token. Tài khoản được phép đăng nhập ngay; xác minh email chưa được triển khai. Giới hạn 10 lần / 60 phút theo IP; vượt ngưỡng trả 429.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/login", async Task<Results<Ok<AuthTokenResponse>, UnauthorizedHttpResult>> (
            LoginRequest request,
            IAuthService authService,
            CancellationToken cancellationToken) =>
        {
            var tokens = await authService.LoginAsync(request, cancellationToken);
            return tokens is null ? TypedResults.Unauthorized() : TypedResults.Ok(tokens);
        })
            .AllowAnonymous()
            .RequireRateLimiting(AuthRateLimits.Login)
            .WithName("Login")
            .WithSummary("Đăng nhập")
            .WithDescription("Kiểm tra thông tin đăng nhập và cấp access token cùng refresh token. Sai mật khẩu 5 lần thì tài khoản bị khoá 15 phút; ngoài ra giới hạn 30 lần / 5 phút theo IP, vượt ngưỡng trả 429.")
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/refresh", async Task<Results<Ok<AuthTokenResponse>, UnauthorizedHttpResult>> (
            RefreshTokenRequest request,
            IAuthService authService,
            CancellationToken cancellationToken) =>
        {
            var tokens = await authService.RefreshAsync(request.RefreshToken, cancellationToken);
            return tokens is null ? TypedResults.Unauthorized() : TypedResults.Ok(tokens);
        })
            .AllowAnonymous()
            .WithName("RefreshTokens")
            .WithSummary("Làm mới token")
            .WithDescription("Sử dụng refresh token còn hiệu lực để cấp cặp token mới. Vai trò và phạm vi chi nhánh trong access token mới được đọc lại từ dữ liệu hiện tại.")
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/logout", async Task<NoContent> (
            RevokeTokenRequest request,
            IAuthService authService,
            CancellationToken cancellationToken) =>
        {
            await authService.RevokeAsync(request.RefreshToken, cancellationToken);
            return TypedResults.NoContent();
        })
            .AllowAnonymous()
            .WithName("Logout")
            .WithSummary("Đăng xuất và thu hồi refresh token")
            .WithDescription("Thu hồi refresh token được gửi. Gọi lặp lại là an toàn; access token hiện tại vẫn dùng được đến khi hết hạn.")
            .Produces(StatusCodes.Status204NoContent);

        group.MapPost("/forgot-password", async Task<Results<Accepted<ForgotPasswordResponse>, ProblemHttpResult>> (
            ForgotPasswordRequest request,
            IAuthService authService,
            ForgotPasswordEmailLimiter emailLimiter,
            CancellationToken cancellationToken) =>
        {
            if (!emailLimiter.TryAcquire(request.Email))
            {
                return TooManyRequests();
            }

            await authService.RequestPasswordResetAsync(request, cancellationToken);
            return TypedResults.Accepted((string?)null, ForgotPasswordResponse.Accepted);
        })
            .AllowAnonymous()
            .RequireRateLimiting(AuthRateLimits.ForgotPassword)
            .WithName("ForgotPassword")
            .WithSummary("Quên mật khẩu: gửi liên kết đặt lại qua email")
            .WithDescription("Luôn trả 202 với cùng một nội dung, dù email có đăng ký hay không (không để lộ tài khoản). Nếu tài khoản tồn tại, máy chủ gửi email chứa liên kết `{App__PublicBaseUrl}{đường dẫn theo app}?email=…&token=…`: `customer` → `/reset-password`, `admin` → `/admin/reset-password`, `pos` → `/pos/reset-password`. Liên kết hết hạn sau `PasswordReset__TokenMinutes` phút (mặc định 15). Giới hạn 5 yêu cầu / 15 phút theo IP và theo email; vượt ngưỡng trả 429.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/reset-password", async Task<Results<NoContent, ValidationProblem, ProblemHttpResult>> (
            ResetPasswordRequest request,
            IAuthService authService,
            CancellationToken cancellationToken) =>
        {
            var result = await authService.ResetPasswordAsync(request, cancellationToken);
            return result.Status switch
            {
                PasswordUpdateStatus.Succeeded => TypedResults.NoContent(),
                PasswordUpdateStatus.ValidationFailed => TypedResults.ValidationProblem(result.Errors.ToDictionary()),
                _ => InvalidResetLink(),
            };
        })
            .AllowAnonymous()
            .RequireRateLimiting(AuthRateLimits.ResetPassword)
            .WithName("ResetPassword")
            .WithSummary("Đặt lại mật khẩu bằng liên kết trong email")
            .WithDescription("Đặt mật khẩu mới từ `email` và `token` của liên kết. Email sai, token sai, đã dùng hoặc hết hạn đều trả cùng một lỗi 400 \"Liên kết không hợp lệ hoặc đã hết hạn\" (`code` = `INVALID_RESET_LINK`). Mật khẩu yếu trả ValidationProblem. Thành công (204): token hết hiệu lực, mở khoá do nhập sai mật khẩu, **đăng xuất mọi phiên** (mọi refresh token cũ trả 401) và gửi email thông báo. Giới hạn 10 yêu cầu / 15 phút theo IP.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/change-password", async Task<Results<NoContent, ValidationProblem, UnauthorizedHttpResult>> (
            ChangePasswordRequest request,
            HttpContext httpContext,
            IAuthService authService,
            CancellationToken cancellationToken) =>
        {
            var result = await authService.ChangePasswordAsync(httpContext.User, request, cancellationToken);
            return result switch
            {
                null => TypedResults.Unauthorized(),
                { Status: PasswordUpdateStatus.Succeeded } => TypedResults.NoContent(),
                _ => TypedResults.ValidationProblem(result.Errors.ToDictionary()),
            };
        })
            .RequireAuthorization()
            .RequireRateLimiting(AuthRateLimits.ChangePassword)
            .WithName("ChangePassword")
            .WithSummary("Đổi mật khẩu")
            .WithDescription("Đổi mật khẩu của tài khoản đang đăng nhập. Mật khẩu hiện tại sai trả 400 trên trường `currentPassword` (không tính vào khoá đăng nhập); mật khẩu mới yếu hoặc trùng mật khẩu cũ trả ValidationProblem. Thành công (204): đăng xuất mọi phiên khác — phiên ứng với `refreshToken` gửi lên được giữ lại — và gửi email thông báo. Access token đã cấp vẫn dùng được đến khi hết hạn (15 phút). Giới hạn 5 lần / 15 phút cho mỗi tài khoản; vượt ngưỡng trả 429.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapGet("/me", async Task<Results<Ok<AuthUserResponse>, NotFound>> (
            HttpContext httpContext,
            IAuthService authService,
            CancellationToken cancellationToken) =>
        {
            var user = await authService.GetCurrentUserAsync(httpContext.User, cancellationToken);
            return user is null ? TypedResults.NotFound() : TypedResults.Ok(user);
        })
            .RequireAuthorization()
            .WithName("GetCurrentUser")
            .WithSummary("Lấy thông tin tài khoản hiện tại")
            .WithDescription("Trả về thông tin an toàn và vai trò hiện tại của tài khoản đã đăng nhập.");
    }

    public const string InvalidResetLinkMessage = "Liên kết không hợp lệ hoặc đã hết hạn";

    private static ProblemHttpResult InvalidResetLink() => TypedResults.Problem(
        statusCode: StatusCodes.Status400BadRequest,
        title: InvalidResetLinkMessage,
        detail: "Hãy yêu cầu gửi lại liên kết đặt lại mật khẩu.",
        extensions: new Dictionary<string, object?> { ["code"] = "INVALID_RESET_LINK" });

    private static ProblemHttpResult TooManyRequests() => TypedResults.Problem(
        statusCode: StatusCodes.Status429TooManyRequests,
        title: "Quá nhiều yêu cầu",
        detail: AuthRateLimits.TooManyRequestsDetail);
}
