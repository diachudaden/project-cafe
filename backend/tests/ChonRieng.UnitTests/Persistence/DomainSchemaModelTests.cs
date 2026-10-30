using ChonRieng.Domain.Entities;
using ChonRieng.Application.Persistence;
using ChonRieng.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace ChonRieng.UnitTests.Persistence;

public sealed class DomainSchemaModelTests
{
    private static readonly string[] BusinessTables =
    [
        "Branches",
        "Zones",
        "Seats",
        "PricePlans",
        "Employees",
        "Customers",
        "Shifts",
        "ShiftAssignments",
        "Attendances",
        "ShiftClosings",
        "Sessions",
        "SessionExtensions",
        "Bills",
        "PointTransactions",
        "Expenses",
        "Payrolls",
        "EmployeeBranches",
        "Availabilities",
        "ShiftRequests",
        "Notifications",
        "UserSessions",
        "Users",
    ];

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=localhost;Database=CafeCrModelTests;Integrated Security=True;TrustServerCertificate=True")
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public void Model_maps_the_22_business_tables_and_identity_tables()
    {
        using var db = CreateContext();

        var tables = db.Model.GetEntityTypes()
            .Select(entity => entity.GetTableName())
            .Where(name => name is not null)
            .ToHashSet(StringComparer.Ordinal);

        Assert.All(BusinessTables, table => Assert.Contains(table, tables));
        Assert.Equal(28, tables.Count);
        Assert.Contains("AspNetRoles", tables);
        Assert.Contains("AspNetUserRoles", tables);
        Assert.Contains("AspNetUserClaims", tables);
        Assert.Contains("AspNetUserLogins", tables);
        Assert.Contains("AspNetUserTokens", tables);
        Assert.Contains("AspNetRoleClaims", tables);
    }

    [Fact]
    public void Business_tables_use_guid_primary_keys()
    {
        using var db = CreateContext();

        var businessEntities = db.Model.GetEntityTypes()
            .Where(entity => BusinessTables.Contains(entity.GetTableName(), StringComparer.Ordinal))
            .ToArray();

        Assert.Equal(22, businessEntities.Length);
        Assert.All(businessEntities, entity =>
        {
            var primaryKey = Assert.Single(entity.FindPrimaryKey()!.Properties);
            Assert.Equal(typeof(Guid), primaryKey.ClrType);
        });
    }

    [Fact]
    public void Monetary_columns_use_vnd_precision_and_timestamps_use_datetimeoffset()
    {
        using var db = CreateContext();

        Assert.Equal("decimal(12,0)", db.Model.FindEntityType(typeof(Bill))!
            .FindProperty(nameof(Bill.Total))!.GetColumnType());
        Assert.Equal("decimal(12,0)", db.Model.FindEntityType(typeof(Expense))!
            .FindProperty(nameof(Expense.Amount))!.GetColumnType());
        Assert.Equal("datetimeoffset", db.Model.FindEntityType(typeof(Session))!
            .FindProperty(nameof(Session.StartAt))!.GetColumnType());
        Assert.Equal("date", db.Model.FindEntityType(typeof(Payroll))!
            .FindProperty(nameof(Payroll.Month))!.GetColumnType());
    }

    [Fact]
    public void Required_uniqueness_and_concurrency_constraints_are_mapped()
    {
        using var db = CreateContext();

        var bill = db.Model.FindEntityType(typeof(Bill))!;
        Assert.Contains(bill.GetIndexes(), index =>
            index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual([nameof(Bill.Code)]));
        Assert.Contains(bill.GetIndexes(), index =>
            index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual([nameof(Bill.SessionId)]));

        var userSession = db.Model.FindEntityType(typeof(UserSession))!;
        Assert.Contains(userSession.GetIndexes(), index =>
            index.IsUnique && index.GetFilter() == "[RevokedAt] IS NULL AND [IsPos] = 1");
        Assert.True(userSession.FindProperty(nameof(UserSession.RefreshTokenHash))!.IsConcurrencyToken);
        Assert.True(userSession.FindProperty(nameof(UserSession.ExpiresAt))!.IsNullable);

        var shift = db.Model.FindEntityType(typeof(Shift))!;
        Assert.True(shift.FindProperty(nameof(Shift.RowVersion))!.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, shift.FindProperty(nameof(Shift.RowVersion))!.ValueGenerated);
    }

    [Fact]
    public void Model_can_generate_schema_sql_without_connecting_to_a_database()
    {
        using var db = CreateContext();

        var sql = db.Database.GenerateCreateScript();

        Assert.Contains("CREATE TABLE [Bills]", sql, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE [ShiftClosings]", sql, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE [Users]", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("PaymentTransactions", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("LedgerEntries", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Branch_scope_query_filters_related_entities_without_connecting_to_a_database()
    {
        using var db = CreateContext();
        var branchId = Guid.CreateVersion7();

        var query = BranchAccessScope.ForBranches(new HashSet<Guid> { branchId })
            .Apply(db.Seats.AsNoTracking(), seat => seat.Zone.BranchId);
        var sql = query.ToQueryString();

        Assert.Contains("INNER JOIN [Zones]", sql, StringComparison.Ordinal);
        Assert.Contains("BranchId", sql, StringComparison.Ordinal);
        Assert.Contains(branchId.ToString(), sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Schema_migrations_are_discoverable_in_order()
    {
        using var db = CreateContext();

        var migrations = db.Database.GetMigrations().ToArray();

        Assert.Equal(2, migrations.Length);
        Assert.EndsWith("_InitialCafeCrSchema", migrations[0], StringComparison.Ordinal);
        Assert.EndsWith("_AddRefreshTokenExpiry", migrations[1], StringComparison.Ordinal);
    }
}
