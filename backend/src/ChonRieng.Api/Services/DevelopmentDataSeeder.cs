using System.Security.Cryptography;
using ChonRieng.Domain.Entities;
using ChonRieng.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ChonRieng.Api.Services;

public static class DevelopmentDataSeeder
{
    private const string SeedPasswordKey = "DevelopmentSeed:Password";
    private const string BranchAName = "[DEMO DATA] Chốn Riêng - Chi nhánh A";
    private const string BranchBName = "[DEMO DATA] Chốn Riêng - Chi nhánh B";
    private const string ManagerEmail = "manager.demo@cafecr.invalid";
    private const string StaffEmail = "staff.demo@cafecr.invalid";
    private const string CustomerEmail = "customer.demo@cafecr.invalid";

    public static async Task SeedAsync(
        IServiceProvider services,
        IConfiguration configuration,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var password = configuration[SeedPasswordKey];
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Set DevelopmentSeed__Password in the local environment before running the development seed.");
        }

        var bootstrapAdminEmail = configuration["BootstrapAdmin:Email"];
        if (string.IsNullOrWhiteSpace(bootstrapAdminEmail))
        {
            throw new InvalidOperationException(
                "Set BootstrapAdmin__Email and BootstrapAdmin__Password before running the development seed.");
        }

        var dbContext = services.GetRequiredService<AppDbContext>();
        var userManager = services.GetRequiredService<UserManager<User>>();
        var admin = await userManager.FindByEmailAsync(bootstrapAdminEmail);
        if (admin is null || !await userManager.IsInRoleAsync(admin, BusinessRoles.Admin))
        {
            throw new InvalidOperationException(
                "The configured bootstrap administrator was not found or does not have the Admin role.");
        }

        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        var branchA = await EnsureBranchAsync(dbContext, BranchAName, "Địa chỉ dữ liệu mẫu A", cancellationToken);
        var branchB = await EnsureBranchAsync(dbContext, BranchBName, "Địa chỉ dữ liệu mẫu B", cancellationToken);
        var zoneA = await EnsureZoneAsync(dbContext, branchA, "[DEMO DATA] Khu A", cancellationToken);
        var zoneB = await EnsureZoneAsync(dbContext, branchB, "[DEMO DATA] Khu B", cancellationToken);

        await EnsureSeatAsync(dbContext, zoneA, "DEMO-A-01", cancellationToken);
        await EnsureSeatAsync(dbContext, zoneA, "DEMO-A-02", cancellationToken);
        await EnsureSeatAsync(dbContext, zoneB, "DEMO-B-01", cancellationToken);
        await EnsureSeatAsync(dbContext, zoneB, "DEMO-B-02", cancellationToken);

        await EnsurePricePlanAsync(dbContext, branchA, cancellationToken);
        await EnsurePricePlanAsync(dbContext, branchB, cancellationToken);

        var manager = await EnsureSeedUserAsync(
            userManager,
            ManagerEmail,
            "[DEMO DATA] Branch Manager",
            BusinessRoles.BranchManager,
            password);
        var staff = await EnsureSeedUserAsync(
            userManager,
            StaffEmail,
            "[DEMO DATA] Staff",
            BusinessRoles.Staff,
            password);
        var customer = await EnsureSeedUserAsync(
            userManager,
            CustomerEmail,
            "[DEMO DATA] Customer",
            BusinessRoles.Customer,
            password);

        var managerEmployee = await EnsureEmployeeAsync(dbContext, userManager, manager, cancellationToken);
        var staffEmployee = await EnsureEmployeeAsync(dbContext, userManager, staff, cancellationToken);
        await EnsureEmployeeBranchAsync(dbContext, managerEmployee.Id, branchA.Id, cancellationToken);
        await EnsureEmployeeBranchAsync(dbContext, staffEmployee.Id, branchA.Id, cancellationToken);
        await EnsureCustomerAsync(dbContext, customer, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        logger.LogInformation(
            "Development seed completed. Demo branches: {BranchA}, {BranchB}. Demo accounts: {ManagerEmail}, {StaffEmail}, {CustomerEmail}.",
            BranchAName,
            BranchBName,
            ManagerEmail,
            StaffEmail,
            CustomerEmail);
    }

    private static async Task<Branch> EnsureBranchAsync(
        AppDbContext dbContext,
        string name,
        string address,
        CancellationToken cancellationToken)
    {
        var branch = await dbContext.Branches.SingleOrDefaultAsync(
            item => item.Name == name,
            cancellationToken);
        if (branch is not null)
        {
            return branch;
        }

        branch = new Branch
        {
            Name = name,
            Address = address,
            Is24h = false,
            OpenTime = new TimeOnly(8, 0),
            CloseTime = new TimeOnly(23, 0),
            IsActive = true,
        };
        dbContext.Branches.Add(branch);
        return branch;
    }

    private static async Task<Zone> EnsureZoneAsync(
        AppDbContext dbContext,
        Branch branch,
        string name,
        CancellationToken cancellationToken)
    {
        var zone = await dbContext.Zones.SingleOrDefaultAsync(
            item => item.BranchId == branch.Id && item.Name == name,
            cancellationToken);
        if (zone is not null)
        {
            return zone;
        }

        zone = new Zone { BranchId = branch.Id, Name = name };
        dbContext.Zones.Add(zone);
        return zone;
    }

    private static async Task EnsureSeatAsync(
        AppDbContext dbContext,
        Zone zone,
        string code,
        CancellationToken cancellationToken)
    {
        var exists = await dbContext.Seats.AnyAsync(
            item => item.ZoneId == zone.Id && item.Code == code,
            cancellationToken);
        if (exists)
        {
            return;
        }

        dbContext.Seats.Add(new Seat
        {
            ZoneId = zone.Id,
            Code = code,
            Type = SeatType.Single,
            Status = SeatStatus.Available,
        });
    }

    private static async Task EnsurePricePlanAsync(
        AppDbContext dbContext,
        Branch branch,
        CancellationToken cancellationToken)
    {
        const string name = "[DEMO ONLY] Gói 240 phút - giá mẫu, không phải giá kinh doanh";
        var exists = await dbContext.PricePlans.AnyAsync(
            item => item.BranchId == branch.Id && item.Name == name,
            cancellationToken);
        if (exists)
        {
            return;
        }

        dbContext.PricePlans.Add(new PricePlan
        {
            BranchId = branch.Id,
            Name = name,
            DurationMinutes = 240,
            Price = 1000m,
            ExtendPrice = 250m,
        });
    }

    private static async Task<User> EnsureSeedUserAsync(
        UserManager<User> userManager,
        string email,
        string fullName,
        string role,
        string password)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new User
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = fullName,
            };
            var createResult = await userManager.CreateAsync(user, password);
            EnsureIdentitySuccess(createResult, $"create the demo account {email}");
        }
        else if (!string.Equals(user.FullName, fullName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"The demo account email {email} is already used by a non-seed account; no account was changed.");
        }

        var roles = await userManager.GetRolesAsync(user);
        if (roles.Any(existingRole => !string.Equals(existingRole, role, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"The demo account {email} has an unexpected role assignment; no account roles were changed.");
        }

        if (!roles.Contains(role, StringComparer.Ordinal))
        {
            var roleResult = await userManager.AddToRoleAsync(user, role);
            EnsureIdentitySuccess(roleResult, $"assign the {role} role to the demo account {email}");
        }

        return user;
    }

    private static async Task<Employee> EnsureEmployeeAsync(
        AppDbContext dbContext,
        UserManager<User> userManager,
        User user,
        CancellationToken cancellationToken)
    {
        var employee = await dbContext.Employees.SingleOrDefaultAsync(
            item => item.UserId == user.Id,
            cancellationToken);
        if (employee is not null)
        {
            return employee;
        }

        var generatedPin = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        employee = new Employee
        {
            UserId = user.Id,
            HourlyRate = 0,
            NightAllowance = 0,
            PinHash = userManager.PasswordHasher.HashPassword(user, generatedPin),
        };
        dbContext.Employees.Add(employee);
        return employee;
    }

    private static async Task EnsureEmployeeBranchAsync(
        AppDbContext dbContext,
        Guid employeeId,
        Guid branchId,
        CancellationToken cancellationToken)
    {
        var assignedBranchIds = await dbContext.EmployeeBranches
            .Where(item => item.EmployeeId == employeeId)
            .Select(item => item.BranchId)
            .ToArrayAsync(cancellationToken);
        if (assignedBranchIds.Any(assignedBranchId => assignedBranchId != branchId))
        {
            throw new InvalidOperationException(
                "A demo employee has an unexpected branch assignment; no branch assignments were changed.");
        }

        if (assignedBranchIds.Contains(branchId))
        {
            return;
        }

        dbContext.EmployeeBranches.Add(new EmployeeBranch
        {
            EmployeeId = employeeId,
            BranchId = branchId,
            IsHome = true,
        });
    }

    private static async Task EnsureCustomerAsync(
        AppDbContext dbContext,
        User user,
        CancellationToken cancellationToken)
    {
        var exists = await dbContext.Customers.AnyAsync(
            item => item.UserId == user.Id,
            cancellationToken);
        if (exists)
        {
            return;
        }

        dbContext.Customers.Add(new Customer
        {
            UserId = user.Id,
            Tier = "DEMO",
            Points = 0,
            TotalSpent = 0,
        });
    }

    private static void EnsureIdentitySuccess(IdentityResult result, string operation)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Could not {operation}: {string.Join(", ", result.Errors.Select(error => error.Code))}");
        }
    }
}
