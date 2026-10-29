using ChonRieng.Domain.Auth;
using ChonRieng.Infrastructure.Auth;
using ChonRieng.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace ChonRieng.Infrastructure.Persistence.Seed;

public static class AuthDataSeeder
{
    public static readonly Guid SampleBranchId = Guid.Parse("11111111-1111-7111-8111-111111111111");

    public static async Task SeedAsync(
        UserManager<ApplicationUser> userManager,
        IOptions<AuthSeedOptions> seedOptions)
    {
        var password = seedOptions.Value.Password;
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        await EnsureUserAsync(
            userManager,
            "admin@chonrieng.local",
            phone: null,
            fullName: "Chủ chuỗi",
            role: AppRoles.Admin,
            branchIdsJson: "[]",
            password);

        await EnsureUserAsync(
            userManager,
            "manager@chonrieng.local",
            phone: "0901111111",
            fullName: "Quản lý chi nhánh",
            role: AppRoles.BranchManager,
            branchIdsJson: $"[\"{SampleBranchId}\"]",
            password);

        await EnsureUserAsync(
            userManager,
            "staff@chonrieng.local",
            phone: "0901234567",
            fullName: "Nhân viên quầy",
            role: AppRoles.Staff,
            branchIdsJson: $"[\"{SampleBranchId}\"]",
            password);

        await EnsureUserAsync(
            userManager,
            "customer@chonrieng.local",
            phone: "0909999999",
            fullName: "Khách thành viên",
            role: AppRoles.Customer,
            branchIdsJson: "[]",
            password);

        await EnsureUserAsync(
            userManager,
            "lockout@chonrieng.local",
            phone: null,
            fullName: "Tài khoản kiểm thử khóa",
            role: AppRoles.Customer,
            branchIdsJson: "[]",
            password);
    }

    private static async Task EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string? phone,
        string fullName,
        string role,
        string branchIdsJson,
        string password)
    {
        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            return;
        }

        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            PhoneNumber = phone,
            PhoneNumberConfirmed = phone is not null,
            FullName = fullName,
            Role = role,
            BranchIdsJson = branchIdsJson,
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to seed user {email}: {errors}");
        }
    }
}
