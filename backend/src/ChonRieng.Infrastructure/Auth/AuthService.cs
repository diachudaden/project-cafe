using ChonRieng.Application.Common;
using ChonRieng.Application.Features.Auth;
using ChonRieng.Application.Features.Auth.Dtos;
using ChonRieng.Domain.Auth;
using ChonRieng.Domain.Entities;
using ChonRieng.Infrastructure.Identity;
using ChonRieng.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ChonRieng.Infrastructure.Auth;

public sealed class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _db;
    private readonly IJwtTokenService _jwt;
    private readonly IClock _clock;
    private readonly AuthCookieOptions _authOptions;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        AppDbContext db,
        IJwtTokenService jwt,
        IClock clock,
        IOptions<AuthCookieOptions> authOptions)
    {
        _userManager = userManager;
        _db = db;
        _jwt = jwt;
        _clock = clock;
        _authOptions = authOptions.Value;
    }

    public async Task<AuthOutcome> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var identifier = request.Identifier?.Trim();
        var password = request.Password;
        var client = request.Client?.Trim();

        if (string.IsNullOrWhiteSpace(identifier) ||
            string.IsNullOrWhiteSpace(password) ||
            !AuthClients.IsKnown(client))
        {
            return AuthOutcome.Fail(400, AuthErrorTypes.Validation, AuthErrorDetails.Validation);
        }

        var user = await FindUserAsync(identifier, cancellationToken);
        if (user is null)
        {
            return AuthOutcome.Fail(401, AuthErrorTypes.InvalidCredentials, AuthErrorDetails.InvalidCredentials);
        }

        if (await _userManager.IsLockedOutAsync(user))
        {
            return AuthOutcome.Fail(401, AuthErrorTypes.AccountLocked, AuthErrorDetails.AccountLocked);
        }

        if (!await _userManager.CheckPasswordAsync(user, password))
        {
            await _userManager.AccessFailedAsync(user);
            if (await _userManager.IsLockedOutAsync(user))
            {
                return AuthOutcome.Fail(401, AuthErrorTypes.AccountLocked, AuthErrorDetails.AccountLocked);
            }

            return AuthOutcome.Fail(401, AuthErrorTypes.InvalidCredentials, AuthErrorDetails.InvalidCredentials);
        }

        if (!AppAccess.Allows(client!, user.Role))
        {
            return AuthOutcome.Fail(403, AuthErrorTypes.ForbiddenClient, AuthErrorDetails.ForbiddenClient);
        }

        await _userManager.ResetAccessFailedCountAsync(user);

        if (client == AuthClients.Pos)
        {
            await RevokePosSessionsAsync(user.Id, cancellationToken);
        }

        return await IssueSessionAsync(user, client!, cancellationToken);
    }

    public async Task<AuthOutcome> RefreshAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        var session = await FindActiveSessionAsync(refreshToken, cancellationToken);
        if (session is null)
        {
            return AuthOutcome.Fail(401, AuthErrorTypes.SessionRevoked, AuthErrorDetails.SessionRevoked);
        }

        var user = await _userManager.FindByIdAsync(session.UserId.ToString());
        if (user is null)
        {
            session.RevokedAt = _clock.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            return AuthOutcome.Fail(401, AuthErrorTypes.SessionRevoked, AuthErrorDetails.SessionRevoked);
        }

        session.RevokedAt = _clock.UtcNow;
        return await IssueSessionAsync(user, session.Client, cancellationToken);
    }

    public async Task<AuthOutcome> LogoutAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        var session = await FindSessionByTokenAsync(refreshToken, cancellationToken);
        if (session is not null && session.RevokedAt is null)
        {
            session.RevokedAt = _clock.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return AuthOutcome.LoggedOut();
    }

    public async Task<AuthUserDto?> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _userManager.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        return user is null ? null : UserMapper.ToDto(user);
    }

    private async Task<ApplicationUser?> FindUserAsync(string identifier, CancellationToken cancellationToken)
    {
        if (identifier.Contains('@', StringComparison.Ordinal))
        {
            return await _userManager.FindByEmailAsync(identifier);
        }

        var phone = PhoneNumberNormalizer.Normalize(identifier);
        if (phone is null)
        {
            return null;
        }

        return await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phone, cancellationToken);
    }

    private async Task<UserSession?> FindActiveSessionAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        var session = await FindSessionByTokenAsync(refreshToken, cancellationToken);
        if (session is null || !session.IsActive(_clock.UtcNow))
        {
            return null;
        }

        return session;
    }

    private async Task<UserSession?> FindSessionByTokenAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return null;
        }

        var hash = RefreshTokenHasher.Hash(refreshToken);
        return await _db.UserSessions.FirstOrDefaultAsync(s => s.RefreshTokenHash == hash, cancellationToken);
    }

    private async Task RevokePosSessionsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var sessions = await _db.UserSessions
            .Where(s => s.UserId == userId && s.Client == AuthClients.Pos && s.RevokedAt == null)
            .ToListAsync(cancellationToken);

        var now = _clock.UtcNow;
        foreach (var session in sessions)
        {
            session.RevokedAt = now;
        }
    }

    private async Task<AuthOutcome> IssueSessionAsync(
        ApplicationUser user,
        string client,
        CancellationToken cancellationToken)
    {
        var refreshToken = RefreshTokenHasher.CreateToken();
        var now = _clock.UtcNow;
        _db.UserSessions.Add(new UserSession
        {
            UserId = user.Id,
            Client = client,
            RefreshTokenHash = RefreshTokenHasher.Hash(refreshToken),
            CreatedAt = now,
            ExpiresAt = now.AddDays(_authOptions.RefreshTokenDays),
        });
        await _db.SaveChangesAsync(cancellationToken);

        var dto = UserMapper.ToDto(user);
        var response = new AuthResponse
        {
            AccessToken = _jwt.CreateAccessToken(dto),
            ExpiresIn = _jwt.AccessTokenSeconds,
            User = dto,
        };
        return AuthOutcome.Ok(response, refreshToken);
    }
}
