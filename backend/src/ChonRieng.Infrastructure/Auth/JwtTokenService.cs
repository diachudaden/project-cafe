using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ChonRieng.Application.Common;
using ChonRieng.Application.Features.Auth;
using ChonRieng.Application.Features.Auth.Dtos;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ChonRieng.Infrastructure.Auth;

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;
    private readonly IClock _clock;

    public JwtTokenService(IOptions<JwtOptions> options, IClock clock)
    {
        _options = options.Value;
        _clock = clock;
    }

    public int AccessTokenSeconds => _options.AccessTokenSeconds;

    public string CreateAccessToken(AuthUserDto user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var now = _clock.UtcNow;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new("role", user.Role),
            new(ClaimTypes.Role, user.Role),
            new("fullName", user.FullName),
            new("branchIds", System.Text.Json.JsonSerializer.Serialize(user.BranchIds)),
        };

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Email, user.Email));
        }

        if (!string.IsNullOrWhiteSpace(user.Phone))
        {
            claims.Add(new Claim("phone", user.Phone));
        }

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: now.UtcDateTime.AddSeconds(_options.AccessTokenSeconds),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
