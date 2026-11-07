using System.Security.Cryptography;
using ChonRieng.Api.Features.Sales;
using ChonRieng.Domain.Entities;
using ChonRieng.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ChonRieng.Api.Services;

/// <summary>
/// Opt-in demo data (TASK-005, extended by the roles proposal): accounts for every role, five branches,
/// and — on the first run only — consistent shift, sales, closing, expense and payroll history.
/// Lookups use reserved .invalid emails and [DEMO DATA] names; non-seed records are never changed.
/// </summary>
public static class DevelopmentDataSeeder
{
    private const string SeedPasswordKey = "DevelopmentSeed:Password";
    private const string HistoryFromKey = "DevelopmentSeed:HistoryFrom";
    private const string BranchAName = "[DEMO DATA] Chốn Riêng - Chi nhánh A";
    private const string BranchBName = "[DEMO DATA] Chốn Riêng - Chi nhánh B";
    private const string ManagerEmail = "manager.demo@cafecr.invalid";
    private const string StaffEmail = "staff.demo@cafecr.invalid";
    private const string CustomerEmail = "customer.demo@cafecr.invalid";
    private const string LegacyPlanName = "[DEMO ONLY] Gói 240 phút - giá mẫu, không phải giá kinh doanh";
    internal const string ChainPlanName = "[DEMO ONLY] Gói 4 tiếng - giá mẫu";
    internal const decimal ChainPlanPrice = 45_000m;
    internal const decimal ChainPlanExtendPrice = 40_000m;

    internal sealed record BranchSpec(string Key, string Name, string Address, decimal Lat, decimal Lng, bool Is24h, TimeOnly Open, TimeOnly Close, string Phone);

    internal static readonly BranchSpec[] BranchSpecs =
    [
        new("A", BranchAName, "Địa chỉ dữ liệu mẫu A", 10.787000m, 106.693000m, false, new(8, 0), new(23, 0), "0280000001"),
        new("B", BranchBName, "Địa chỉ dữ liệu mẫu B", 10.823100m, 106.629700m, false, new(8, 0), new(23, 0), "0280000002"),
        new("C", "[DEMO DATA] Chốn Riêng - Chi nhánh C", "Địa chỉ dữ liệu mẫu C", 10.776900m, 106.700900m, true, new(0, 0), new(0, 0), "0280000003"),
        new("D", "[DEMO DATA] Chốn Riêng - Chi nhánh D", "Địa chỉ dữ liệu mẫu D", 10.801600m, 106.714800m, true, new(0, 0), new(0, 0), "0280000004"),
        new("E", "[DEMO DATA] Chốn Riêng - Chi nhánh E", "Địa chỉ dữ liệu mẫu E", 10.762600m, 106.660200m, false, new(7, 0), new(22, 0), "0280000005"),
    ];

    // Synthetic names; with the [DEMO DATA] prefix they are the stable non-seed conflict check.
    private static readonly string[] AdminNames = ["Trần Minh Quân", "Lê Thu Hà", "Phạm Đức Anh"];

    // Two managers per branch (A–E); manager10 also covers branch A to exercise multi-branch scope.
    private static readonly string[] ManagerNames =
    [
        "Hoàng Ngọc Mai", "Vũ Quốc Bảo", "Đinh Thảo Vy", "Quách Minh Tâm", "Kiều Anh Tuấn",
        "Mạc Thị Yến", "Tôn Đức Long", "La Bảo Châu", "Âu Gia Hưng", "Hà Thanh Xuân",
    ];

    // Four staff per branch (01–04 A, 05–08 B, …); staff04 also works at B and staff08 at C.
    private static readonly string[] StaffNames =
    [
        "Nguyễn Văn An", "Trần Thị Bình", "Lê Hoàng Cường", "Phạm Thị Dung", "Đỗ Minh Đức",
        "Bùi Thị Giang", "Đặng Văn Hải", "Ngô Thị Hương", "Dương Quang Khải", "Lý Thị Lan",
        "Hồ Văn Mạnh", "Mai Thị Nga", "Trịnh Văn Oai", "Phan Thị Phương", "Võ Minh Quang",
        "Tạ Thị Rạng", "Lâm Văn Sang", "Châu Thị Tuyết", "Huỳnh Văn Út", "Trương Thị Vân",
    ];

    private static readonly string[] CustomerNames =
    [
        "Nguyễn Hoài Nam", "Trần Bảo Ngọc", "Lê Gia Huy", "Phạm Khánh Linh", "Hoàng Tuấn Kiệt",
        "Vũ Phương Thảo", "Đỗ Thành Long", "Bùi Thanh Trúc", "Đặng Hữu Phước", "Ngô Mỹ Duyên",
        "Dương Minh Khoa", "Lý Hồng Nhung", "Hồ Đăng Khoa", "Mai Anh Thư", "Trịnh Công Sơn",
        "Phan Ngọc Hân", "Võ Nhật Minh", "Tạ Thu Trang", "Lâm Gia Bảo", "Châu Kim Ngân",
    ];

    internal const decimal StaffHourlyRate = 25_000m;
    internal const decimal ManagerHourlyRate = 35_000m;
    internal const decimal DemoNightAllowance = 10_000m;

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
        var clock = services.GetService<TimeProvider>() ?? TimeProvider.System;
        DateOnly? historyFrom = null;
        if (configuration[HistoryFromKey] is { Length: > 0 } historyFromValue)
        {
            if (!DateOnly.TryParseExact(historyFromValue, "yyyy-MM-dd", out var parsed) || parsed > DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime))
            {
                throw new InvalidOperationException("DevelopmentSeed__HistoryFrom must be a past date in yyyy-MM-dd format, e.g. 2026-09-01.");
            }

            historyFrom = parsed;
        }
        var admin = await userManager.FindByEmailAsync(bootstrapAdminEmail);
        if (admin is null || !await userManager.IsInRoleAsync(admin, BusinessRoles.Admin))
        {
            throw new InvalidOperationException(
                "The configured bootstrap administrator was not found or does not have the Admin role.");
        }

        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        var now = clock.GetUtcNow();
        var branches = new List<Branch>();
        foreach (var spec in BranchSpecs)
        {
            branches.Add(await EnsureBranchAsync(dbContext, spec, cancellationToken));
        }

        var (branchA, branchB) = (branches[0], branches[1]);
        await EnsureFloorAsync(dbContext, branches, cancellationToken);
        await EnsurePricePlansAsync(dbContext, branches, now, cancellationToken);

        // Original TASK-005 accounts, unchanged.
        var manager = await EnsureSeedUserAsync(userManager, ManagerEmail, "[DEMO DATA] Branch Manager", BusinessRoles.BranchManager, password);
        var staff = await EnsureSeedUserAsync(userManager, StaffEmail, "[DEMO DATA] Staff", BusinessRoles.Staff, password);
        var customer = await EnsureSeedUserAsync(userManager, CustomerEmail, "[DEMO DATA] Customer", BusinessRoles.Customer, password);
        var managerEmployee = await EnsureEmployeeAsync(dbContext, userManager, manager, 0, cancellationToken);
        var staffEmployee = await EnsureEmployeeAsync(dbContext, userManager, staff, 0, cancellationToken);
        await EnsureEmployeeBranchesAsync(dbContext, managerEmployee.Id, branchA.Id, [], cancellationToken);
        await EnsureEmployeeBranchesAsync(dbContext, staffEmployee.Id, branchA.Id, [], cancellationToken);
        await EnsureCustomerAsync(dbContext, customer, "DEMO", null, cancellationToken);

        for (var i = 0; i < AdminNames.Length; i++)
        {
            await EnsureSeedUserAsync(userManager, DemoEmail("admin", i), $"[DEMO DATA] {AdminNames[i]}", BusinessRoles.Admin, password);
        }

        var managers = new List<(Employee Employee, int Branch)>();
        for (var i = 0; i < ManagerNames.Length; i++)
        {
            var user = await EnsureSeedUserAsync(userManager, DemoEmail("manager", i), $"[DEMO DATA] {ManagerNames[i]}", BusinessRoles.BranchManager, password);
            var employee = await EnsureEmployeeAsync(dbContext, userManager, user, ManagerHourlyRate, cancellationToken);
            var home = i / 2;
            await EnsureEmployeeBranchesAsync(dbContext, employee.Id, branches[home].Id, i == 9 ? [branchA.Id] : [], cancellationToken);
            managers.Add((employee, home));
        }

        var staffMembers = new List<(Employee Employee, int Branch)>();
        for (var i = 0; i < StaffNames.Length; i++)
        {
            var user = await EnsureSeedUserAsync(userManager, DemoEmail("staff", i), $"[DEMO DATA] {StaffNames[i]}", BusinessRoles.Staff, password);
            var employee = await EnsureEmployeeAsync(dbContext, userManager, user, StaffHourlyRate, cancellationToken);
            var home = i / 4;
            Guid[] extra = i switch { 3 => [branchB.Id], 7 => [branches[2].Id], _ => [] };
            await EnsureEmployeeBranchesAsync(dbContext, employee.Id, branches[home].Id, extra, cancellationToken);
            staffMembers.Add((employee, home));
        }

        var customers = new List<Customer>();
        for (var i = 0; i < CustomerNames.Length; i++)
        {
            var user = await EnsureSeedUserAsync(userManager, DemoEmail("customer", i), $"[DEMO DATA] {CustomerNames[i]}", BusinessRoles.Customer, password);
            customers.Add(await EnsureCustomerAsync(dbContext, user, Loyalty.Bronze, $"CRDEMO{i + 1:00}", cancellationToken));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        var created = await DevelopmentHistorySeeder.SeedAsync(dbContext, branches, managers, staffMembers, customers, now, historyFrom, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        logger.LogInformation(
            "Development seed completed: {BranchCount} demo branches; accounts {AdminCount} admin, {ManagerCount} managers, {StaffCount} staff, {CustomerCount} customers (plus {ManagerEmail}, {StaffEmail}, {CustomerEmail}). {History}",
            branches.Count,
            AdminNames.Length,
            ManagerNames.Length,
            StaffNames.Length,
            CustomerNames.Length,
            ManagerEmail,
            StaffEmail,
            CustomerEmail,
            created);
    }

    internal static string DemoEmail(string prefix, int index) => $"{prefix}{index + 1:00}.demo@cafecr.invalid";

    private static async Task<Branch> EnsureBranchAsync(AppDbContext dbContext, BranchSpec spec, CancellationToken cancellationToken)
    {
        var branch = await dbContext.Branches.SingleOrDefaultAsync(item => item.Name == spec.Name, cancellationToken);
        if (branch is not null)
        {
            // Seed-owned rows may gain map coordinates added after TASK-005; nothing else is changed.
            branch.Latitude ??= spec.Lat;
            branch.Longitude ??= spec.Lng;
            branch.Phone ??= spec.Phone;
            return branch;
        }

        branch = new Branch
        {
            Name = spec.Name,
            Address = spec.Address,
            Latitude = spec.Lat,
            Longitude = spec.Lng,
            Is24h = spec.Is24h,
            OpenTime = spec.Open,
            CloseTime = spec.Close,
            Phone = spec.Phone,
            IsActive = true,
        };
        dbContext.Branches.Add(branch);
        return branch;
    }

    private static async Task EnsureFloorAsync(AppDbContext dbContext, IReadOnlyList<Branch> branches, CancellationToken cancellationToken)
    {
        for (var b = 0; b < branches.Count; b++)
        {
            var key = BranchSpecs[b].Key;
            var branch = branches[b];
            // Branches A/B keep their TASK-005 zone and seats; everything else is added alongside.
            var quiet = await EnsureZoneAsync(dbContext, branch, b < 2 ? $"[DEMO DATA] Khu {key}" : "[DEMO DATA] Khu yên tĩnh", cancellationToken);
            var group = await EnsureZoneAsync(dbContext, branch, "[DEMO DATA] Khu nhóm", cancellationToken);
            var singles = key switch { "C" => 8, "D" => 7, "E" => 6, _ => 6 };
            for (var i = 1; i <= singles; i++)
            {
                await EnsureSeatAsync(dbContext, quiet, $"DEMO-{key}-{i:00}", SeatType.Single, SeatStatus.Available, cancellationToken);
            }

            var groups = key is "C" or "D" ? 3 : 2;
            for (var i = 1; i <= groups; i++)
            {
                await EnsureSeatAsync(dbContext, group, $"DEMO-{key}-G{i}", SeatType.Group,
                    key == "D" && i == groups ? SeatStatus.Maintenance : SeatStatus.Available, cancellationToken);
            }

            if (key is "A" or "C")
            {
                var nap = key == "C" ? await EnsureZoneAsync(dbContext, branch, "[DEMO DATA] Góc ngủ", cancellationToken) : quiet;
                await EnsureSeatAsync(dbContext, nap, $"DEMO-{key}-N1", SeatType.Nap, SeatStatus.Available, cancellationToken);
                if (key == "C")
                {
                    await EnsureSeatAsync(dbContext, nap, $"DEMO-{key}-N2", SeatType.Nap, SeatStatus.Maintenance, cancellationToken);
                }
            }
        }
    }

    private static async Task<Zone> EnsureZoneAsync(AppDbContext dbContext, Branch branch, string name, CancellationToken cancellationToken)
    {
        var zone = dbContext.Zones.Local.SingleOrDefault(item => item.BranchId == branch.Id && item.Name == name)
            ?? await dbContext.Zones.SingleOrDefaultAsync(item => item.BranchId == branch.Id && item.Name == name, cancellationToken);
        if (zone is not null)
        {
            return zone;
        }

        zone = new Zone { BranchId = branch.Id, Name = name };
        dbContext.Zones.Add(zone);
        return zone;
    }

    private static async Task EnsureSeatAsync(AppDbContext dbContext, Zone zone, string code, SeatType type, SeatStatus status, CancellationToken cancellationToken)
    {
        if (dbContext.Seats.Local.Any(item => item.ZoneId == zone.Id && item.Code == code)
            || await dbContext.Seats.AnyAsync(item => item.ZoneId == zone.Id && item.Code == code, cancellationToken))
        {
            return;
        }

        dbContext.Seats.Add(new Seat { ZoneId = zone.Id, Code = code, Type = type, Status = status });
    }

    private static async Task EnsurePricePlansAsync(AppDbContext dbContext, IReadOnlyList<Branch> branches, DateTimeOffset now, CancellationToken cancellationToken)
    {
        // The TASK-005 1.000đ plans would override the chain plan at A/B; close them (seed-owned rows only).
        foreach (var legacy in await dbContext.PricePlans.Where(p => p.Name == LegacyPlanName && p.ValidTo == null).ToListAsync(cancellationToken))
        {
            legacy.ValidTo = now;
        }

        await EnsurePlanAsync(dbContext, null, ChainPlanName, ChainPlanPrice, ChainPlanExtendPrice, null, null, cancellationToken);
        await EnsurePlanAsync(dbContext, branches[2].Id, "[DEMO ONLY] Gói 4 tiếng chi nhánh C - giá mẫu", 40_000m, 35_000m, null, null, cancellationToken);
        await EnsurePlanAsync(dbContext, branches[3].Id, "[DEMO ONLY] Khuyến mãi đã hết hạn - giá mẫu", 30_000m, 30_000m,
            now.AddMonths(-3), now.AddMonths(-2), cancellationToken);
    }

    private static async Task EnsurePlanAsync(AppDbContext dbContext, Guid? branchId, string name, decimal price, decimal extendPrice,
        DateTimeOffset? validFrom, DateTimeOffset? validTo, CancellationToken cancellationToken)
    {
        if (await dbContext.PricePlans.AnyAsync(item => item.BranchId == branchId && item.Name == name, cancellationToken))
        {
            return;
        }

        dbContext.PricePlans.Add(new PricePlan
        {
            BranchId = branchId,
            Name = name,
            DurationMinutes = 240,
            Price = price,
            ExtendPrice = extendPrice,
            ValidFrom = validFrom,
            ValidTo = validTo,
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
        decimal hourlyRate,
        CancellationToken cancellationToken)
    {
        var userId = user.Id;
        var employee = await dbContext.Employees.SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (employee is not null)
        {
            return employee;
        }

        // PIN login is a later phase (decision D2); the PIN is random and discarded.
        var generatedPin = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        employee = new Employee
        {
            UserId = user.Id,
            HourlyRate = hourlyRate,
            NightAllowance = hourlyRate == 0 ? 0 : DemoNightAllowance,
            PinHash = userManager.PasswordHasher.HashPassword(user, generatedPin),
        };
        dbContext.Employees.Add(employee);
        return employee;
    }

    private static async Task EnsureEmployeeBranchesAsync(
        AppDbContext dbContext,
        Guid employeeId,
        Guid homeBranchId,
        IReadOnlyCollection<Guid> extraBranchIds,
        CancellationToken cancellationToken)
    {
        var expected = extraBranchIds.Append(homeBranchId).ToHashSet();
        var assigned = await dbContext.EmployeeBranches
            .Where(item => item.EmployeeId == employeeId)
            .Select(item => item.BranchId)
            .ToArrayAsync(cancellationToken);
        if (assigned.Any(branchId => !expected.Contains(branchId)))
        {
            throw new InvalidOperationException(
                "A demo employee has an unexpected branch assignment; no branch assignments were changed.");
        }

        foreach (var branchId in expected.Where(id => !assigned.Contains(id)))
        {
            dbContext.EmployeeBranches.Add(new EmployeeBranch
            {
                EmployeeId = employeeId,
                BranchId = branchId,
                IsHome = branchId == homeBranchId && !assigned.Contains(homeBranchId),
            });
        }
    }

    private static async Task<Customer> EnsureCustomerAsync(
        AppDbContext dbContext,
        User user,
        string tier,
        string? memberCode,
        CancellationToken cancellationToken)
    {
        var userId = user.Id;
        var existing = await dbContext.Customers.SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var customer = new Customer
        {
            UserId = user.Id,
            Tier = tier,
            MemberCode = memberCode,
            Points = 0,
            TotalSpent = 0,
        };
        dbContext.Customers.Add(customer);
        return customer;
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
