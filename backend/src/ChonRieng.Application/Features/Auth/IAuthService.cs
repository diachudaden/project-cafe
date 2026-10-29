using ChonRieng.Application.Features.Auth.Dtos;

namespace ChonRieng.Application.Features.Auth;

public interface IAuthService
{
    Task<AuthOutcome> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<AuthOutcome> RefreshAsync(string? refreshToken, CancellationToken cancellationToken);
    Task<AuthOutcome> LogoutAsync(string? refreshToken, CancellationToken cancellationToken);
    Task<AuthUserDto?> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken);
}
