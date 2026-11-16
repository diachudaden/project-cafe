using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using ChonRieng.Api.Endpoints;
using ChonRieng.Api.Features.Sales;
using ChonRieng.Api.Services.Email;
using ChonRieng.Application.Persistence;
using ChonRieng.Domain.Entities;
using ChonRieng.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ChonRieng.Api.Services;

public sealed class AuthService(
    UserManager<User> userManager,
    SignInManager<User> signInManager,
    AppDbContext dbContext,
    IBranchAccessService branchAccessService,
    ITokenService tokenService,
    IOptions<JwtOptions> jwtOptions,
    IEmailQueue emailQueue,
    IOptions<AppOptions> appOptions,
    IOptions<PasswordResetOptions> passwordResetOptions,
    TimeProvider clock,
    ILogger<AuthService> logger) : IAuthService
{
    private static readonly string[] CustomerRole = [BusinessRoles.Customer];
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;

    public async Task<RegistrationResult> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var user = new User
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName,
            PhoneNumber = request.PhoneNumber,
        };

        var createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            return new RegistrationResult(null, IdentityErrorFormatter.ToValidationErrors(createResult));
        }

        var roleResult = await userManager.AddToRoleAsync(user, BusinessRoles.Customer);
        if (!roleResult.Succeeded)
        {
            return new RegistrationResult(null, IdentityErrorFormatter.ToValidationErrors(roleResult));
        }

        dbContext.Customers.Add(new Customer
        {
            UserId = user.Id,
            Tier = Loyalty.Bronze,
            MemberCode = Loyalty.NewMemberCode(),
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        var tokens = await CreateTokenResponseAsync(user, CustomerRole, request.DeviceId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new RegistrationResult(tokens, new Dictionary<string, string[]>());
    }

    public async Task<AuthTokenResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return null;
        }

        var signInResult = await signInManager.CheckPasswordSignInAsync(
            user,
            request.Password,
            lockoutOnFailure: true);
        if (!signInResult.Succeeded)
        {
            return null;
        }

        var roles = (await userManager.GetRolesAsync(user)).ToArray();
        return await CreateTokenResponseAsync(user, roles, request.DeviceId, cancellationToken);
    }

    public async Task<AuthTokenResponse?> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var tokenHash = tokenService.HashRefreshToken(refreshToken);
        var session = await dbContext.UserSessions.SingleOrDefaultAsync(
            item => item.RefreshTokenHash == tokenHash
                && !item.IsPos
                && item.RevokedAt == null
                && item.ExpiresAt > now,
            cancellationToken);
        if (session is null)
        {
            return null;
        }

        var user = await userManager.FindByIdAsync(session.UserId.ToString());
        if (user is null || await userManager.IsLockedOutAsync(user))
        {
            return null;
        }

        var roles = (await userManager.GetRolesAsync(user)).ToArray();
        var branchIds = await GetBranchIdsAsync(user.Id, roles, cancellationToken);
        var rotatedToken = tokenService.CreateRefreshToken();
        session.RefreshTokenHash = tokenService.HashRefreshToken(rotatedToken);
        session.CreatedAt = now;
        session.ExpiresAt = now.AddDays(_jwtOptions.RefreshTokenDays);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return null;
        }

        return CreateTokenResponse(user, roles, branchIds, rotatedToken, session.ExpiresAt.Value);
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var tokenHash = tokenService.HashRefreshToken(refreshToken);
        var session = await dbContext.UserSessions.SingleOrDefaultAsync(
            item => item.RefreshTokenHash == tokenHash && !item.IsPos && item.RevokedAt == null,
            cancellationToken);
        if (session is null)
        {
            return;
        }

        session.RevokedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<AuthUserResponse?> GetCurrentUserAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (!Guid.TryParse(userId, out var id))
        {
            return null;
        }

        var user = await userManager.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var roles = (await userManager.GetRolesAsync(user)).ToArray();
        var branchIds = await GetBranchIdsAsync(user.Id, roles, cancellationToken);
        return ToUserResponse(user, roles, branchIds);
    }

    public async Task RequestPasswordResetAsync(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        // The caller answers 202 with the same body whatever happens here (no user enumeration).
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null || string.IsNullOrEmpty(user.Email) || IsPermanentlyLocked(user))
        {
            return;
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var link = PasswordEmailTemplates.BuildResetLink(appOptions.Value.PublicBaseUrl, request.App, user.Email, encodedToken);
        var queued = emailQueue.Enqueue(PasswordEmailTemplates.Reset(
            user.Id, user.Email, user.FullName, link, passwordResetOptions.Value.TokenMinutes));
        logger.LogInformation("Password reset requested for user {UserId} via {App}; queued={Queued}.", user.Id, request.App, queued);
    }

    public async Task<PasswordUpdateResult> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null || IsPermanentlyLocked(user) || !TryDecodeToken(request.Token, out var token))
        {
            return PasswordUpdateResult.InvalidLink;
        }

        // Identity checks the token (purpose, lifetime, security stamp) before the password rules, and on success
        // rotates the security stamp, which invalidates this and every other outstanding reset token.
        var result = await userManager.ResetPasswordAsync(user, token, request.NewPassword);
        if (!result.Succeeded)
        {
            return result.Errors.Any(error => error.Code == nameof(IdentityErrorDescriber.InvalidToken))
                ? PasswordUpdateResult.InvalidLink
                : PasswordUpdateResult.Invalid(IdentityErrorFormatter.ToValidationErrors(result));
        }

        // Proving control of the mailbox also clears a lockout caused by wrong passwords.
        await userManager.ResetAccessFailedCountAsync(user);
        await userManager.SetLockoutEndDateAsync(user, null);
        var revoked = await RevokeSessionsAsync(user.Id, keepRefreshToken: null, cancellationToken);
        QueuePasswordChangedEmail(user);
        logger.LogInformation("Password reset completed for user {UserId}; revoked {Sessions} sessions.", user.Id, revoked);
        return PasswordUpdateResult.Success;
    }

    private async Task<int> RevokeSessionsAsync(Guid userId, string? keepRefreshToken, CancellationToken cancellationToken)
    {
        var keepHash = string.IsNullOrEmpty(keepRefreshToken) ? null : tokenService.HashRefreshToken(keepRefreshToken);
        var now = clock.GetUtcNow();
        var sessions = await dbContext.UserSessions
            .Where(item => item.UserId == userId && item.RevokedAt == null)
            .ToListAsync(cancellationToken);
        var revoked = 0;
        foreach (var session in sessions.Where(item => item.RefreshTokenHash != keepHash))
        {
            session.RevokedAt = now;
            revoked++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return revoked;
    }

    private void QueuePasswordChangedEmail(User user)
    {
        if (!string.IsNullOrEmpty(user.Email))
        {
            emailQueue.Enqueue(PasswordEmailTemplates.Changed(user.Id, user.Email, user.FullName, clock.GetUtcNow()));
        }
    }

    /// <summary>A lockout set far in the future is an administrative block, not the 15-minute wrong-password lockout.</summary>
    private bool IsPermanentlyLocked(User user) =>
        user.LockoutEnd is { } end && end > clock.GetUtcNow().AddYears(100);

    private static bool TryDecodeToken(string encoded, out string token)
    {
        try
        {
            token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(encoded));
            return token.Length > 0;
        }
        catch (FormatException)
        {
            token = "";
            return false;
        }
    }

    private async Task<AuthTokenResponse> CreateTokenResponseAsync(
        User user,
        IReadOnlyCollection<string> roles,
        string? deviceId,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var refreshToken = tokenService.CreateRefreshToken();
        var refreshExpiresAt = now.AddDays(_jwtOptions.RefreshTokenDays);
        var branchIds = await GetBranchIdsAsync(user.Id, roles, cancellationToken);
        dbContext.UserSessions.Add(new UserSession
        {
            UserId = user.Id,
            DeviceId = string.IsNullOrWhiteSpace(deviceId) ? "api" : deviceId,
            IsPos = false,
            RefreshTokenHash = tokenService.HashRefreshToken(refreshToken),
            CreatedAt = now,
            ExpiresAt = refreshExpiresAt,
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        return CreateTokenResponse(user, roles, branchIds, refreshToken, refreshExpiresAt);
    }

    private AuthTokenResponse CreateTokenResponse(
        User user,
        IReadOnlyCollection<string> roles,
        IReadOnlyCollection<Guid> branchIds,
        string refreshToken,
        DateTimeOffset refreshExpiresAt)
    {
        var accessExpiresAt = DateTimeOffset.UtcNow.AddMinutes(_jwtOptions.AccessTokenMinutes);
        return new AuthTokenResponse(
            tokenService.CreateAccessToken(user, roles, branchIds),
            accessExpiresAt,
            refreshToken,
            refreshExpiresAt,
            ToUserResponse(user, roles, branchIds));
    }

    private Task<IReadOnlySet<Guid>> GetBranchIdsAsync(
        Guid userId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken)
    {
        return roles.Contains(BusinessRoles.BranchManager, StringComparer.Ordinal)
            || roles.Contains(BusinessRoles.Staff, StringComparer.Ordinal)
            ? branchAccessService.GetAssignedBranchIdsAsync(userId, cancellationToken)
            : Task.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid>());
    }

    private static AuthUserResponse ToUserResponse(
        User user,
        IReadOnlyCollection<string> roles,
        IReadOnlyCollection<Guid> branchIds)
    {
        return new AuthUserResponse(user.Id, user.Email ?? "", user.FullName, user.PhoneNumber, roles.ToArray(), branchIds.ToArray());
    }

}
