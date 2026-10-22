using ChonRieng.Domain.Entities;
using ChonRieng.Domain.Payments;
using ChonRieng.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ChonRieng.UnitTests.Payments;

public sealed class PaymentPersistenceModelTests
{
    [Fact]
    public void Model_contains_payment_tables_and_bootstrap_domain_tables()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=(local);Database=ModelOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        using var db = new AppDbContext(options);

        Assert.Equal("PaymentTransactions", db.Model.FindEntityType(typeof(PaymentTransaction))!.GetTableName());
        Assert.Equal("LedgerEntries", db.Model.FindEntityType(typeof(LedgerEntry))!.GetTableName());
        Assert.Equal("ReconciliationIssues", db.Model.FindEntityType(typeof(ReconciliationIssue))!.GetTableName());
        Assert.Equal("Bills", db.Model.FindEntityType(typeof(Bill))!.GetTableName());
    }

    [Fact]
    public void Seat_row_version_is_concurrency_token()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=(local);Database=ModelOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        using var db = new AppDbContext(options);

        var rowVersion = db.Model.FindEntityType(typeof(Seat))!.FindProperty(nameof(Seat.RowVersion));

        Assert.NotNull(rowVersion);
        Assert.True(rowVersion.IsConcurrencyToken);
        Assert.True(rowVersion.ValueGenerated == Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAddOrUpdate);
    }
}
