using System.Security.Claims;
using ChonRieng.Api.Services;
using ChonRieng.Application.Persistence;
using Microsoft.AspNetCore.Authorization;

namespace ChonRieng.Api.Authorization;

public sealed class CustomerOwnResourceAuthorizationHandler(IBranchAccessService branchAccessService)
    : AuthorizationHandler<CustomerOwnResourceRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        CustomerOwnResourceRequirement requirement)
    {
        if (context.User.IsInRole(BusinessRoles.Admin))
        {
            context.Succeed(requirement);
            return;
        }

        if (!context.User.IsInRole(BusinessRoles.Customer))
        {
            return;
        }

        var httpContext = context.Resource as HttpContext;
        var userIdValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("sub");
        if (httpContext is null || !Guid.TryParse(userIdValue, out var userId))
        {
            return;
        }

        if (Guid.TryParse(httpContext.Request.RouteValues["userId"]?.ToString(), out var resourceUserId)
            && resourceUserId == userId)
        {
            context.Succeed(requirement);
            return;
        }

        if (Guid.TryParse(httpContext.Request.RouteValues["customerId"]?.ToString(), out var customerId)
            && await branchAccessService.IsCustomerOwnedByUserAsync(
                customerId,
                userId,
                httpContext.RequestAborted))
        {
            context.Succeed(requirement);
        }
    }
}
