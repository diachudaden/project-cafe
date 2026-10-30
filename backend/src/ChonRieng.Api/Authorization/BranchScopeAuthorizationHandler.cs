using ChonRieng.Api.Services;
using Microsoft.AspNetCore.Authorization;

namespace ChonRieng.Api.Authorization;

public sealed class BranchScopeAuthorizationHandler : AuthorizationHandler<BranchScopeRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        BranchScopeRequirement requirement)
    {
        if (context.User.IsInRole(BusinessRoles.Admin))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        if (!context.User.IsInRole(BusinessRoles.BranchManager)
            && !context.User.IsInRole(BusinessRoles.Staff))
        {
            return Task.CompletedTask;
        }

        var httpContext = context.Resource as HttpContext;
        if (httpContext?.Request.RouteValues.TryGetValue("branchId", out var routeValue) != true
            || !Guid.TryParse(routeValue?.ToString(), out var branchId))
        {
            return Task.CompletedTask;
        }

        var scope = context.User.GetBranchAccessScope();
        if (scope.BranchIds.Contains(branchId))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
