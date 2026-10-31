using ChonRieng.Api.Services;
using ChonRieng.Domain.Entities;
using ChonRieng.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ChonRieng.UnitTests.Api;

public sealed class DevelopmentDataSeederTests
{
    private const string AdminEmail = "admin.test@cafecr.invalid";
    private const string AdminPassword = "Admin-Test-Only-123!";
    private const string SeedPassword = "Seed-Test-Only-123!";

    [Fact]
    public async Task Seed_is_idempotent_and_preserves_unrelated_records()
    {
        using var provider = CreateProvider();
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        var configuration = CreateConfiguration();
        await RoleBootstrapper.InitializeAsync(
            scope.ServiceProvider,
            configuration,
            NullLogger.Instance);

        dbContext.Branches.Add(new Branch
        {
            Name = "Unrelated Cafe",
            Address = "Keep this record",
            OpenTime = new TimeOnly(8, 0),
            CloseTime = new TimeOnly(23, 0),
        });
        await dbContext.SaveChangesAsync();

        await DevelopmentDataSeeder.SeedAsync(
            scope.ServiceProvider,
            configuration,
            NullLogger.Instance,
            CancellationToken.None);
        dbContext.ChangeTracker.Clear();
        var firstRunCounts = await GetCountsAsync(dbContext);

        await DevelopmentDataSeeder.SeedAsync(
            scope.ServiceProvider,
            configuration,
            NullLogger.Instance,
            CancellationToken.None);
        dbContext.ChangeTracker.Clear();
        var secondRunCounts = await GetCountsAsync(dbContext);

        Assert.Equal(firstRunCounts, secondRunCounts);
        // Users: bootstrap admin + 3 named demo accounts + 5 admins + 10 staff + 20 customers.
        Assert.Equal((3, 2, 4, 2, 12, 12, 21, 39), secondRunCounts);

        var unrelatedBranch = await dbContext.Branches.SingleAsync(item => item.Name == "Unrelated Cafe");
        Assert.Equal("Keep this record", unrelatedBranch.Address);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var admin = await dbContext.Users.SingleAsync(item => item.Email == AdminEmail);
        Assert.Equal([BusinessRoles.Admin], await userManager.GetRolesAsync(admin));

        var manager = await dbContext.Users.SingleAsync(item => item.Email == "manager.demo@cafecr.invalid");
        var staff = await dbContext.Users.SingleAsync(item => item.Email == "staff.demo@cafecr.invalid");
        var customer = await dbContext.Users.SingleAsync(item => item.Email == "customer.demo@cafecr.invalid");
        Assert.Equal([BusinessRoles.BranchManager], await userManager.GetRolesAsync(manager));
        Assert.Equal([BusinessRoles.Staff], await userManager.GetRolesAsync(staff));
        Assert.Equal([BusinessRoles.Customer], await userManager.GetRolesAsync(customer));

        var branchA = await dbContext.Branches.SingleAsync(item => item.Name == "[DEMO DATA] Chốn Riêng - Chi nhánh A");
        var scopedEmployeeIds = await dbContext.Employees
            .Where(item => item.UserId == manager.Id || item.UserId == staff.Id)
            .Select(item => item.Id)
            .ToArrayAsync();
        foreach (var employeeId in scopedEmployeeIds)
        {
            var assignedBranchIds = await dbContext.EmployeeBranches
                .Where(item => item.EmployeeId == employeeId)
                .Select(item => item.BranchId)
                .ToArrayAsync();
            Assert.Equal([branchA.Id], assignedBranchIds);
        }

        var customerProfile = await dbContext.Customers.SingleAsync(item => item.UserId == customer.Id);
        Assert.Equal("DEMO", customerProfile.Tier);

        Assert.Equal(6, (await userManager.GetUsersInRoleAsync(BusinessRoles.Admin)).Count);
        Assert.Equal(11, (await userManager.GetUsersInRoleAsync(BusinessRoles.Staff)).Count);
        Assert.Equal(21, (await userManager.GetUsersInRoleAsync(BusinessRoles.Customer)).Count);

        var bulkStaffBranches = await dbContext.EmployeeBranches
            .Where(item => item.Employee.User.Email!.StartsWith("staff") && item.Employee.User.Email != "staff.demo@cafecr.invalid")
            .GroupBy(item => item.BranchId)
            .Select(group => new { BranchId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.BranchId, item => item.Count);
        Assert.Equal(5, bulkStaffBranches[branchA.Id]);

        var customer20 = await dbContext.Users.SingleAsync(item => item.Email == "customer20.demo@cafecr.invalid");
        Assert.True(await dbContext.Customers.AnyAsync(item => item.UserId == customer20.Id));

        Assert.All(
            await dbContext.Seats.ToArrayAsync(),
            seat => Assert.Equal(SeatStatus.Available, seat.Status));
        Assert.All(
            await dbContext.PricePlans.Where(item => item.Name.StartsWith("[DEMO ONLY]")).ToArrayAsync(),
            plan => Assert.Equal(1000m, plan.Price));

        var managerEmployeeId = await dbContext.Employees
            .Where(item => item.UserId == manager.Id)
            .Select(item => item.Id)
            .SingleAsync();
        var branchB = await dbContext.Branches.SingleAsync(item => item.Name == "[DEMO DATA] Chốn Riêng - Chi nhánh B");
        dbContext.EmployeeBranches.Add(new EmployeeBranch
        {
            EmployeeId = managerEmployeeId,
            BranchId = branchB.Id,
            IsHome = false,
        });
        await dbContext.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DevelopmentDataSeeder.SeedAsync(
                scope.ServiceProvider,
                configuration,
                NullLogger.Instance,
                CancellationToken.None));
    }

    [Fact]
    public async Task Seed_requires_a_local_password_before_writing_demo_records()
    {
        using var provider = CreateProvider();
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        var configuration = CreateConfiguration(includeSeedPassword: false);
        await RoleBootstrapper.InitializeAsync(
            scope.ServiceProvider,
            configuration,
            NullLogger.Instance);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DevelopmentDataSeeder.SeedAsync(
                scope.ServiceProvider,
                configuration,
                NullLogger.Instance,
                CancellationToken.None));

        Assert.Contains("DevelopmentSeed__Password", exception.Message, StringComparison.Ordinal);
        Assert.Empty(await dbContext.Branches.ToListAsync());
    }

    [Fact]
    public void Seed_command_is_rejected_outside_development_and_testing()
    {
        Assert.Throws<InvalidOperationException>(() =>
            DevelopmentSeedCommand.EnsureAllowed(new TestHostEnvironment(Environments.Production)));

        DevelopmentSeedCommand.EnsureAllowed(new TestHostEnvironment(Environments.Development));
        DevelopmentSeedCommand.EnsureAllowed(new TestHostEnvironment("Testing"));
        Assert.True(DevelopmentSeedCommand.IsRequested(["--seed-development-data"]));
        Assert.False(DevelopmentSeedCommand.IsRequested(["--help"]));
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase($"DevelopmentSeederTests-{Guid.NewGuid()}"));
        services.AddIdentityCore<User>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>();
        return services.BuildServiceProvider();
    }

    private static IConfiguration CreateConfiguration(bool includeSeedPassword = true)
    {
        var values = new Dictionary<string, string?>
        {
            ["BootstrapAdmin:Email"] = AdminEmail,
            ["BootstrapAdmin:Password"] = AdminPassword,
            ["BootstrapAdmin:FullName"] = "[TEST] Admin",
        };
        if (includeSeedPassword)
        {
            values["DevelopmentSeed:Password"] = SeedPassword;
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static async Task<(int Branches, int Zones, int Seats, int PricePlans, int Employees, int EmployeeBranches, int Customers, int Users)>
        GetCountsAsync(AppDbContext dbContext)
    {
        return (
            await dbContext.Branches.CountAsync(),
            await dbContext.Zones.CountAsync(),
            await dbContext.Seats.CountAsync(),
            await dbContext.PricePlans.CountAsync(),
            await dbContext.Employees.CountAsync(),
            await dbContext.EmployeeBranches.CountAsync(),
            await dbContext.Customers.CountAsync(),
            await dbContext.Users.CountAsync());
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "ChonRieng.UnitTests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
