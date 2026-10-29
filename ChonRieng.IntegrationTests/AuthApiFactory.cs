using System.Security.Cryptography;
using ChonRieng.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace ChonRieng.IntegrationTests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AuthApiSerialCollection : ICollectionFixture<AuthApiFactory>
{
    public const string Name = "AuthApiSerial";
}

public sealed class AuthApiFactory : WebApplicationFactory<Program>
{
    public string TestPassword { get; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(18)) + "aA1!";

    private SqliteConnection? _connection;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Jwt:SigningKey", Convert.ToHexString(RandomNumberGenerator.GetBytes(48)));
        builder.UseSetting("AuthSeed:Password", TestPassword);
        builder.ConfigureServices(services =>
        {
            var toRemove = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>)
                    || d.ServiceType == typeof(AppDbContext)
                    || d.ServiceType == typeof(IDbContextOptionsConfiguration<AppDbContext>))
                .ToList();
            foreach (var descriptor in toRemove)
            {
                services.Remove(descriptor);
            }

            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _connection?.Dispose();
        }

        base.Dispose(disposing);
    }
}
