using System.Data;
using System.Data.Common;
using ChonRieng.Api.Features.Catalog;
using ChonRieng.Api.Features.Common;
using ChonRieng.Api.Features.Finance;
using ChonRieng.Api.Features.Reports;
using ChonRieng.Api.Features.Sales;
using ChonRieng.Api.Features.Scheduling;
using ChonRieng.Api.Services;
using ChonRieng.Application.Persistence;
using ChonRieng.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace ChonRieng.UnitTests.Features;

/// <summary>
/// Runs feature queries through the SQL Server provider without a server: an interceptor answers every
/// command with an empty result. A LINQ expression SQL Server cannot translate fails here, which the
/// in-memory workflow tests cannot detect.
/// </summary>
public sealed class SqlTranslationTests
{
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly CurrentUser Admin = new(Guid.NewGuid(), new HashSet<string> { BusinessRoles.Admin }, BranchAccessScope.Global);
    private static readonly DateOnly Today = new(2026, 11, 2);

    public static TheoryData<string> Queries() =>
    [
        "branches", "availability", "plans", "managed-plans", "shifts", "my-shifts", "open-shifts", "attendance", "current-shift",
        "active-sessions", "customer-lookup", "my-bills", "my-session", "closings", "expenses", "payroll", "requests", "my-requests",
        "employees", "assistant", "revenue", "profit-loss", "occupancy", "compare", "dashboard",
        "auto-release", "recent-closed", "my-bill-detail",
    ];

    [Theory]
    [MemberData(nameof(Queries))]
    public async Task Feature_queries_translate_to_sql(string name)
    {
        var interceptor = new FakeServer();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=fake;Database=fake;TrustServerCertificate=True")
            .AddInterceptors(interceptor)
            .Options);
        var clock = TimeProvider.System;
        var shifts = new ShiftService(db, clock);
        var reports = new ReportService(db, clock);
        var finance = new FinanceService(db, shifts, clock);
        var workforce = new WorkforceService(db, shifts, clock);

        Func<Task> query = name switch
        {
            "branches" => () => new CatalogService(db, clock).ListBranchesAsync(10.7, 106.6, default),
            "availability" => () => new CatalogService(db, clock).GetAvailabilityAsync(BranchId, default),
            "plans" => () => new CatalogService(db, clock).GetActivePlansAsync(BranchId, default),
            "managed-plans" => () => new CatalogService(db, clock).ListManagedPlansAsync(Admin, BranchId, default),
            "shifts" => () => shifts.ListAsync(Admin, BranchId, Today, Today.AddDays(6), default),
            "my-shifts" => () => shifts.MyShiftsAsync(Admin, Today, Today.AddDays(6), default),
            "open-shifts" => () => shifts.OpenShiftsAsync(Admin, default),
            "attendance" => () => new AttendanceService(db, clock).ListAsync(Admin, BranchId, Today, Today, default),
            "current-shift" => () => new AttendanceService(db, clock).CurrentAsync(Admin, default),
            "active-sessions" => () => new SessionService(db, clock).ActiveAsync(Admin, BranchId, default),
            "customer-lookup" => () => new SessionService(db, clock).LookupCustomersAsync("0900000000", default),
            "my-bills" => () => new LoyaltyService(db, clock).BillsAsync(Admin, default),
            "my-session" => () => new LoyaltyService(db, clock).CurrentSessionAsync(Admin, default),
            "closings" => () => finance.ListClosingsAsync(Admin, BranchId, Today, Today, null, default),
            "expenses" => () => finance.ListExpensesAsync(Admin, BranchId, Today, Today, default),
            "payroll" => () => finance.ListPayrollAsync(Admin, BranchId, Today, default),
            "requests" => () => workforce.ListRequestsAsync(Admin, BranchId, "Pending", default),
            "my-requests" => () => workforce.MyRequestsAsync(Admin, default),
            "employees" => () => workforce.ListEmployeesAsync(Admin, BranchId, default),
            "assistant" => () => workforce.AssistantAsync(Admin, BranchId, Today, Today.AddDays(6), default),
            "revenue" => () => reports.RevenueAsync(Admin, BranchId, ReportPeriod.Month, Today, default),
            "profit-loss" => () => reports.ProfitLossAsync(Admin, null, ReportPeriod.Quarter, Today, default),
            "occupancy" => () => reports.OccupancyAsync(Admin, BranchId, ReportPeriod.Week, Today, default),
            "compare" => () => reports.CompareBranchesAsync(Admin, ReportPeriod.Year, Today, default),
            "dashboard" => () => reports.DashboardAsync(Admin, null, default),
            // Candidate query + the conditional ExecuteUpdate (TASK-024).
            "auto-release" => async () =>
            {
                var release = new SessionAutoReleaseService(db, clock,
                    Microsoft.Extensions.Options.Options.Create(new SessionReleaseOptions()),
                    Microsoft.Extensions.Logging.Abstractions.NullLogger<SessionAutoReleaseService>.Instance);
                await release.FindCandidatesAsync(clock.GetUtcNow(), default);
                Assert.False(await release.TryReleaseAsync(new AutoReleaseCandidate(Guid.NewGuid(), clock.GetUtcNow(), "A-01", "B", null), clock.GetUtcNow(), default));
                Assert.Contains(interceptor.Commands, c => c.Contains("UPDATE", StringComparison.Ordinal) && c.Contains("[EndAt] <=", StringComparison.Ordinal));
            },
            "my-bill-detail" => () => new LoyaltyService(db, clock).BillDetailAsync(Admin, "ABCD1234", default),
            "recent-closed" => () => new SessionService(db, clock).RecentClosedAsync(Admin, BranchId, 2, default),
            _ => throw new ArgumentOutOfRangeException(nameof(name)),
        };

        await query();
        Assert.NotEmpty(interceptor.Commands);
    }

    /// <summary>Pretends to be SQL Server: opens nothing and returns empty rows (or a single 0/false scalar row).</summary>
    private sealed class FakeServer : DbCommandInterceptor, IDbConnectionInterceptor
    {
        public List<string> Commands { get; } = [];

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Commands.Add(command.CommandText);
            var table = new DataTable();
            var sql = command.CommandText.TrimStart();
            if (sql.StartsWith("SELECT CASE", StringComparison.Ordinal))
            {
                table.Columns.Add("c", typeof(bool));
                table.Rows.Add(false);
            }
            else if (sql.StartsWith("SELECT COUNT(", StringComparison.Ordinal))
            {
                table.Columns.Add("c", typeof(int));
                table.Rows.Add(0);
            }
            else if (sql.StartsWith("SELECT COALESCE(SUM(", StringComparison.Ordinal) || sql.StartsWith("SELECT SUM(", StringComparison.Ordinal))
            {
                table.Columns.Add("c", typeof(decimal));
                table.Rows.Add(0m);
            }

            return ValueTask.FromResult(InterceptionResult<DbDataReader>.SuppressWithResult(table.CreateDataReader()));
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Commands.Add(command.CommandText);
            return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(0));
        }

        public ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(InterceptionResult.Suppress());

        public InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result) =>
            InterceptionResult.Suppress();
    }
}
