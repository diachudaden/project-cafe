using ChonRieng.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace ChonRieng.IntegrationTests;

/// <summary>
/// A disposable SQL Server (Testcontainers) with every migration applied. Needed where the in-memory provider
/// cannot prove behaviour: rowversion concurrency, filtered unique indexes and SQL translation. Requires Docker.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2025-latest")
        .Build();

    public string ConnectionString { get; private set; } = "";

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = "CafeCrInventoryTests",
            TrustServerCertificate = true,
        };
        ConnectionString = builder.ConnectionString;
        await using var db = NewContext();
        await db.Database.MigrateAsync();
    }

    public AppDbContext NewContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(ConnectionString).Options);

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "SQL Server";
}
