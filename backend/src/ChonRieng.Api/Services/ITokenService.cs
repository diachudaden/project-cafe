using ChonRieng.Domain.Entities;

namespace ChonRieng.Api.Services;

public interface ITokenService
{
    string CreateAccessToken(
        User user,
        IReadOnlyCollection<string> roles,
        IReadOnlyCollection<Guid>? branchIds = null);

    string CreateRefreshToken();

    string HashRefreshToken(string refreshToken);
}
