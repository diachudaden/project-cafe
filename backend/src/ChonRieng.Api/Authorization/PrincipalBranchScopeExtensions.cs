using System.Security.Claims;
using ChonRieng.Api.Services;
using ChonRieng.Application.Persistence;

namespace ChonRieng.Api.Authorization;

public static class PrincipalBranchScopeExtensions
{
    public static BranchAccessScope GetBranchAccessScope(this ClaimsPrincipal principal)
    {
        if (principal.IsInRole(BusinessRoles.Admin))
        {
            return BranchAccessScope.Global;
        }

        var branchIds = principal.FindAll(BusinessRoles.BranchIdClaim)
            .Select(claim => claim.Value)
            .Where(value => Guid.TryParse(value, out _))
            .Select(Guid.Parse)
            .ToHashSet();
        return BranchAccessScope.ForBranches(branchIds);
    }
}
