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
            .WithName("RegisterCustomer")
            .WithSummary("Đăng ký tài khoản khách hàng")
            .WithDescription("Tạo tài khoản khách hàng, hồ sơ khách hàng và trả về access token cùng refresh token. Tài khoản được phép đăng nhập ngay; xác minh email chưa được triển khai.")
            .ProducesValidationProblem();

        group.MapPost("/login", async Task<Results<Ok<AuthTokenResponse>, UnauthorizedHttpResult>> (
            LoginRequest request,
            IAuthService authService,
            CancellationToken cancellationToken) =>
        {
            var tokens = await authService.LoginAsync(request, cancellationToken);
            return tokens is null ? TypedResults.Unauthorized() : TypedResults.Ok(tokens);
        })
            .AllowAnonymous()
            .WithName("Login")
            .WithSummary("Đăng nhập")
            .WithDescription("Kiểm tra thông tin đăng nhập và cấp access token cùng refresh token.")
            .Produces(StatusCodes.Status401Unauthorized);

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
}
