using ChonRieng.Api.Services;
using Microsoft.AspNetCore.Authorization;

namespace ChonRieng.Api.Features.Common;

/// <summary>Role-only policies; branch and ownership checks are enforced again inside each service.</summary>
public static class Policies
{
    public const string Workforce = "Workforce";
    public const string Management = "Management";
    public const string CustomerOnly = "CustomerOnly";
    public const string AdminOnly = "AdminOnly";

    public static void AddFeaturePolicies(this AuthorizationOptions options)
    {
        options.AddPolicy(Workforce, p => p.RequireRole(BusinessRoles.Admin, BusinessRoles.BranchManager, BusinessRoles.Staff));
        options.AddPolicy(Management, p => p.RequireRole(BusinessRoles.Admin, BusinessRoles.BranchManager));
        options.AddPolicy(CustomerOnly, p => p.RequireRole(BusinessRoles.Customer));
        options.AddPolicy(AdminOnly, p => p.RequireRole(BusinessRoles.Admin));
    }

    public static TBuilder RequireWorkforce<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder =>
        builder.RequireAuthorization(Workforce);

    public static TBuilder RequireManagement<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder =>
        builder.RequireAuthorization(Management);

    public static TBuilder RequireCustomer<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder =>
        builder.RequireAuthorization(CustomerOnly);
}
