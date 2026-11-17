using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ChonRieng.Api.Authorization;
using ChonRieng.Api.Services;
using ChonRieng.Application.Persistence;

namespace ChonRieng.Api.Features.Common;

/// <summary>Identity, roles and branch scope of the caller, read from the signed access token.</summary>
public sealed record CurrentUser(Guid UserId, IReadOnlySet<string> Roles, BranchAccessScope Scope)
{
    public bool IsAdmin => Roles.Contains(BusinessRoles.Admin);
    public bool IsManager => Roles.Contains(BusinessRoles.BranchManager);
    public bool IsStaff => Roles.Contains(BusinessRoles.Staff);
    public bool IsCustomer => Roles.Contains(BusinessRoles.Customer);

    /// <summary>Admin anywhere; Manager/Staff only in branches from their token.</summary>
    public bool CanAccessBranch(Guid branchId) =>
        IsAdmin || ((IsManager || IsStaff) && Scope.BranchIds.Contains(branchId));

    /// <summary>Management actions (scheduling, approvals, finance): Admin anywhere, Manager in own branches.</summary>
    public bool CanManageBranch(Guid branchId) =>
        IsAdmin || (IsManager && Scope.BranchIds.Contains(branchId));

    public static CurrentUser From(ClaimsPrincipal principal)
    {
        var id = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (!Guid.TryParse(id, out var userId))
        {
            throw new InvalidOperationException("The access token does not contain a user id.");
        }

        var roles = principal.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToHashSet(StringComparer.Ordinal);
        return new CurrentUser(userId, roles, principal.GetBranchAccessScope());
    }
}
