using ChonRieng.Application.Features.Auth.Dtos;

namespace ChonRieng.Application.Features.Auth;

public interface IJwtTokenService
{
    string CreateAccessToken(AuthUserDto user);
    int AccessTokenSeconds { get; }
}
