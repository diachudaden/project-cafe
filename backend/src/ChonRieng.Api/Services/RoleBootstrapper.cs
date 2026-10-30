using ChonRieng.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace ChonRieng.Api.Services;

public static class RoleBootstrapper
{
    private static readonly string[] Roles = ["Admin", "Customer"];

    public static async Task InitializeAsync(
        IServiceProvider services,
        IConfiguration configuration,
        ILogger logger)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = services.GetRequiredService<UserManager<User>>();

        foreach (var role in Roles)
        {
            if (await roleManager.RoleExistsAsync(role))
            {
                continue;
            }

            var result = await roleManager.CreateAsync(new IdentityRole<Guid>(role));
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not initialize the {role} role: {string.Join("; ", result.Errors.Select(error => error.Code))}");
            }
        }

        var email = configuration["BootstrapAdmin:Email"];
        var password = configuration["BootstrapAdmin:Password"];
        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("No bootstrap administrator is configured. Admin API routes will have no initial administrator.");
            return;
        }

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Set both BootstrapAdmin__Email and BootstrapAdmin__Password to provision the initial administrator.");
        }

        var admin = await userManager.FindByEmailAsync(email);
        if (admin is null)
        {
            admin = new User
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = configuration["BootstrapAdmin:FullName"] ?? "Administrator",
            };
            var result = await userManager.CreateAsync(admin, password);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not provision the bootstrap administrator: {string.Join("; ", result.Errors.Select(error => error.Code))}");
            }
        }

        if (!await userManager.IsInRoleAsync(admin, "Admin"))
        {
            var result = await userManager.AddToRoleAsync(admin, "Admin");
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not assign the Admin role to the bootstrap account: {string.Join("; ", result.Errors.Select(error => error.Code))}");
            }
        }
    }
}
