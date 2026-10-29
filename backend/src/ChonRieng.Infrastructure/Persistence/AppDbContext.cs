using ChonRieng.Domain.Entities;
using ChonRieng.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ChonRieng.Infrastructure.Persistence;

public sealed class AppDbContext : IdentityUserContext<ApplicationUser, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<UserSession> UserSessions => Set<UserSession>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(u => u.FullName).HasMaxLength(200).IsRequired();
            entity.Property(u => u.Role).HasMaxLength(32).IsRequired();
            entity.Property(u => u.BranchIdsJson).HasMaxLength(2000).IsRequired();
        });

        builder.Entity<UserSession>(entity =>
        {
            entity.ToTable("UserSessions");
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Client).HasMaxLength(32).IsRequired();
            entity.Property(s => s.RefreshTokenHash).HasMaxLength(128).IsRequired();
            entity.HasIndex(s => s.RefreshTokenHash).IsUnique();
            entity.HasIndex(s => new { s.UserId, s.Client, s.RevokedAt });
        });

        builder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens");
        builder.Entity<ApplicationUser>().ToTable("Users");
    }
}
