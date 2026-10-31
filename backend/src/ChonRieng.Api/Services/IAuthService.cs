using ChonRieng.Api.Endpoints;
using System.Security.Claims;

namespace ChonRieng.Api.Services;

public interface IAuthService
{
    Task<RegistrationResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);

    Task<AuthTokenResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    Task<AuthTokenResponse?> RefreshAsync(string refreshToken, CancellationToken cancellationToken);

    Task RevokeAsync(string refreshToken, CancellationToken cancellationToken);

    Task<AuthUserResponse?> GetCurrentUserAsync(ClaimsPrincipal principal, CancellationToken cancellationToken);
}
