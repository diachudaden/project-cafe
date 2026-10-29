using ChonRieng.Application.Common;
using ChonRieng.Application.Features.Auth;
using ChonRieng.Infrastructure.Auth;
using ChonRieng.Infrastructure.Identity;
using ChonRieng.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ChonRieng.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<AuthCookieOptions>(configuration.GetSection(AuthCookieOptions.SectionName));
        services.Configure<AuthSeedOptions>(configuration.GetSection(AuthSeedOptions.SectionName));

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=chonrieng.db";

        services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));

        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Password.RequiredLength = 8;
            })
            .AddEntityFrameworkStores<AppDbContext>();

        services.AddScoped<IAuthService, AuthService>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IClock, SystemClock>();

        return services;
    }
}
