using ChonRieng.Domain.Entities;

namespace ChonRieng.Api.Services;

public interface ITokenService
{
    string CreateAccessToken(User user, IReadOnlyCollection<string> roles);

    string CreateRefreshToken();

    string HashRefreshToken(string refreshToken);
}
