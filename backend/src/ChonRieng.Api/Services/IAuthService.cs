using ChonRieng.Api.Endpoints;
using System.Security.Claims;

namespace ChonRieng.Api.Services;

public interface IAuthService
{
    Task<RegistrationResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);

    Task<AuthTokenResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    Task<AuthTokenResponse?> RefreshAsync(string refreshToken, CancellationToken cancellationToken);

    Task RevokeAsync(string refreshToken, CancellationToken cancellationToken);

    /// <summary>Queues a reset email when the account exists; never reveals whether it does.</summary>
    Task RequestPasswordResetAsync(ForgotPasswordRequest request, CancellationToken cancellationToken);

    Task<PasswordUpdateResult> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken);

    /// <summary>Null when the signed-in account no longer exists.</summary>
    Task<PasswordUpdateResult?> ChangePasswordAsync(ClaimsPrincipal principal, ChangePasswordRequest request, CancellationToken cancellationToken);

    Task<AuthUserResponse?> GetCurrentUserAsync(ClaimsPrincipal principal, CancellationToken cancellationToken);
}
