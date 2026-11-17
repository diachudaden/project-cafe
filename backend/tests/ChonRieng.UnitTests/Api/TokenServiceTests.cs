using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ChonRieng.Api.Services;
using ChonRieng.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace ChonRieng.UnitTests.Api;

public sealed class TokenServiceTests
{
    private const string SigningKey = "a-test-only-signing-key-with-32-bytes-minimum";

    [Fact]
    public void Access_token_contains_identity_and_role_claims_and_validates()
    {
        var options = Options.Create(new JwtOptions
        {
            SigningKey = SigningKey,
            Issuer = "test-issuer",
            Audience = "test-audience",
            AccessTokenMinutes = 15,
        });
        var service = new TokenService(options);
        var user = new User
        {
            Email = "admin@example.test",
            UserName = "admin@example.test",
            FullName = "Test Admin",
        };

        var branchId = Guid.NewGuid();
        var encoded = service.CreateAccessToken(user, ["Admin", "Staff"], [branchId]);
        var validation = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "test-issuer",
            ValidateAudience = true,
            ValidAudience = "test-audience",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            RoleClaimType = ClaimTypes.Role,
        };
        var principal = new JwtSecurityTokenHandler().ValidateToken(encoded, validation, out _);

        Assert.Equal(user.Id.ToString(), principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal("admin@example.test", principal.FindFirstValue(ClaimTypes.Email));
        Assert.Contains(principal.Claims, claim => claim.Type == ClaimTypes.Role && claim.Value == "Admin");
        Assert.Contains(principal.Claims, claim =>
            claim.Type == BusinessRoles.BranchIdClaim && claim.Value == branchId.ToString());
    }

    [Fact]
    public void Refresh_tokens_are_random_and_hashes_are_one_way_deterministic_values()
    {
        var service = new TokenService(Options.Create(new JwtOptions { SigningKey = SigningKey }));

        var first = service.CreateRefreshToken();
        var second = service.CreateRefreshToken();

        Assert.NotEqual(first, second);
        Assert.Equal(service.HashRefreshToken(first), service.HashRefreshToken(first));
        Assert.NotEqual(first, service.HashRefreshToken(first));
    }
}
