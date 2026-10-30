using System.Security.Claims;
using ChonRieng.Api.Authorization;
using ChonRieng.Api.Services;
using ChonRieng.Application.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace ChonRieng.UnitTests.Api;

public sealed class AuthorizationPolicyTests
{
    [Fact]
    public async Task Admin_has_global_branch_access()
    {
        var branchId = Guid.NewGuid();
        var context = CreateContext(
            new BranchScopeRequirement(),
            CreatePrincipal(BusinessRoles.Admin),
            new Dictionary<string, string> { ["branchId"] = branchId.ToString() });

        await new BranchScopeAuthorizationHandler().HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task Branch_manager_can_access_only_branch_ids_in_token_scope()
    {
        var allowedBranchId = Guid.NewGuid();
        var otherBranchId = Guid.NewGuid();
        var handler = new BranchScopeAuthorizationHandler();

        var allowedContext = CreateContext(
            new BranchScopeRequirement(),
            CreatePrincipal(BusinessRoles.BranchManager, allowedBranchId),
            new Dictionary<string, string> { ["branchId"] = allowedBranchId.ToString() });
        await handler.HandleAsync(allowedContext);

        var deniedContext = CreateContext(
            new BranchScopeRequirement(),
            CreatePrincipal(BusinessRoles.BranchManager, allowedBranchId),
            new Dictionary<string, string> { ["branchId"] = otherBranchId.ToString() });
        await handler.HandleAsync(deniedContext);

        Assert.True(allowedContext.HasSucceeded);
        Assert.False(deniedContext.HasSucceeded);
    }

    [Fact]
    public async Task Staff_is_branch_scoped_and_customer_cannot_use_branch_policy()
    {
        var branchId = Guid.NewGuid();
        var handler = new BranchScopeAuthorizationHandler();
        var staffContext = CreateContext(
            new BranchScopeRequirement(),
            CreatePrincipal(BusinessRoles.Staff, branchId),
            new Dictionary<string, string> { ["branchId"] = branchId.ToString() });
        var customerContext = CreateContext(
            new BranchScopeRequirement(),
            CreatePrincipal(BusinessRoles.Customer, branchId),
            new Dictionary<string, string> { ["branchId"] = branchId.ToString() });

        await handler.HandleAsync(staffContext);
        await handler.HandleAsync(customerContext);

        Assert.True(staffContext.HasSucceeded);
        Assert.False(customerContext.HasSucceeded);
    }

    [Fact]
    public async Task Branch_manager_without_an_assigned_branch_claim_is_denied()
    {
        var context = CreateContext(
            new BranchScopeRequirement(),
            CreatePrincipal(BusinessRoles.BranchManager),
            new Dictionary<string, string> { ["branchId"] = Guid.NewGuid().ToString() });

        await new BranchScopeAuthorizationHandler().HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task Customer_can_access_own_user_or_customer_route_only()
    {
        var userId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var handler = new CustomerOwnResourceAuthorizationHandler(
            new FakeBranchAccessService(customerId, userId));

        var ownUserContext = CreateContext(
            new CustomerOwnResourceRequirement(),
            CreatePrincipal(BusinessRoles.Customer, userId: userId),
            new Dictionary<string, string> { ["userId"] = userId.ToString() });
        var ownCustomerContext = CreateContext(
            new CustomerOwnResourceRequirement(),
            CreatePrincipal(BusinessRoles.Customer, userId: userId),
            new Dictionary<string, string> { ["customerId"] = customerId.ToString() });
        var otherUserContext = CreateContext(
            new CustomerOwnResourceRequirement(),
            CreatePrincipal(BusinessRoles.Customer, userId: userId),
            new Dictionary<string, string> { ["userId"] = otherUserId.ToString() });

        await handler.HandleAsync(ownUserContext);
        await handler.HandleAsync(ownCustomerContext);
        await handler.HandleAsync(otherUserContext);

        Assert.True(ownUserContext.HasSucceeded);
        Assert.True(ownCustomerContext.HasSucceeded);
        Assert.False(otherUserContext.HasSucceeded);
    }

    [Fact]
    public async Task Admin_can_access_customer_resource_and_non_customer_roles_cannot()
    {
        var customerId = Guid.NewGuid();
        var accessService = new FakeBranchAccessService(customerId, Guid.NewGuid());
        var handler = new CustomerOwnResourceAuthorizationHandler(accessService);
        var routeValues = new Dictionary<string, string> { ["customerId"] = customerId.ToString() };

        var adminContext = CreateContext(
            new CustomerOwnResourceRequirement(),
            CreatePrincipal(BusinessRoles.Admin),
            routeValues);
        var staffContext = CreateContext(
            new CustomerOwnResourceRequirement(),
            CreatePrincipal(BusinessRoles.Staff),
            routeValues);

        await handler.HandleAsync(adminContext);
        await handler.HandleAsync(staffContext);

        Assert.True(adminContext.HasSucceeded);
        Assert.False(staffContext.HasSucceeded);
    }

    [Fact]
    public void Branch_access_scope_filters_queries_and_admin_scope_is_global()
    {
        var allowedBranchId = Guid.NewGuid();
        var otherBranchId = Guid.NewGuid();
        var rows = new[]
        {
            new BranchRow(allowedBranchId, "allowed"),
            new BranchRow(otherBranchId, "other"),
        }.AsQueryable();

        var scoped = BranchAccessScope.ForBranches(new HashSet<Guid> { allowedBranchId })
            .Apply(rows, row => row.BranchId)
            .ToArray();
        var global = BranchAccessScope.Global
            .Apply(rows, row => row.BranchId)
            .ToArray();

        Assert.Equal([allowedBranchId], scoped.Select(row => row.BranchId));
        Assert.Equal(2, global.Length);
    }

    [Fact]
    public void Branch_scope_is_derived_from_signed_principal_claims()
    {
        var branchId = Guid.NewGuid();
        var principal = CreatePrincipal(BusinessRoles.Staff, branchId);

        var scope = principal.GetBranchAccessScope();

        Assert.False(scope.IsGlobal);
        Assert.Contains(branchId, scope.BranchIds);
        Assert.True(CreatePrincipal(BusinessRoles.Admin).GetBranchAccessScope().IsGlobal);
    }

    private static AuthorizationHandlerContext CreateContext(
        IAuthorizationRequirement requirement,
        ClaimsPrincipal principal,
        IReadOnlyDictionary<string, string> routeValues)
    {
        var httpContext = new DefaultHttpContext();
        foreach (var routeValue in routeValues)
        {
            httpContext.Request.RouteValues[routeValue.Key] = routeValue.Value;
        }

        return new AuthorizationHandlerContext([requirement], principal, httpContext);
    }

    private static ClaimsPrincipal CreatePrincipal(string role, Guid? branchId = null, Guid? userId = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, (userId ?? Guid.NewGuid()).ToString()),
            new(ClaimTypes.Role, role),
        };
        if (branchId is not null)
        {
            claims.Add(new Claim(BusinessRoles.BranchIdClaim, branchId.Value.ToString()));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    private sealed record BranchRow(Guid BranchId, string Value);

    private sealed class FakeBranchAccessService(Guid customerId, Guid userId) : IBranchAccessService
    {
        public Task<IReadOnlySet<Guid>> GetAssignedBranchIdsAsync(
            Guid requestedUserId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid>());
        }

        public Task<bool> IsCustomerOwnedByUserAsync(
            Guid requestedCustomerId,
            Guid requestedUserId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(requestedCustomerId == customerId && requestedUserId == userId);
        }
    }
}
