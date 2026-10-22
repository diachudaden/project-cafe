using ChonRieng.Domain.Entities;
using ChonRieng.Domain.Payments;
using ChonRieng.Infrastructure.Payments;
using Microsoft.EntityFrameworkCore;

namespace ChonRieng.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Bill> Bills => Set<Bill>();
    public DbSet<Seat> Seats => Set<Seat>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();
    public DbSet<ReconciliationIssue> ReconciliationIssues => Set<ReconciliationIssue>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        modelBuilder.Entity<Bill>().ToTable("Bills");
        modelBuilder.Entity<Bill>().HasKey(x => x.Id);
        modelBuilder.Entity<Bill>().Property(x => x.Total).HasColumnType("decimal(18,2)");
        modelBuilder.Entity<Bill>().Property(x => x.Status).HasConversion<byte>();
        modelBuilder.Entity<Bill>().Property(x => x.PaymentMethod).HasConversion<byte?>();
        modelBuilder.Entity<Bill>().Ignore(x => x.NeedsAttention);
        modelBuilder.Entity<Bill>().Ignore(x => x.AttentionReason);

        modelBuilder.Entity<Seat>(b =>
        {
            b.ToTable("Seats");
            b.HasKey(x => x.Id);
            b.Property(x => x.Status).HasConversion<byte>();
            b.Property(x => x.Code).HasMaxLength(50).IsRequired();
            b.Property(x => x.SeatType).HasMaxLength(50).IsRequired();
            b.Property(x => x.RowVersion).IsRowVersion();
            b.HasIndex(x => x.HeldForBillId);
            b.HasIndex(x => new { x.Status, x.HeldUntil });
        });

        modelBuilder.Entity<Session>(b =>
        {
            b.ToTable("Sessions");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).UseIdentityColumn();
        });

        modelBuilder.Entity<Shift>(b =>
        {
            b.ToTable("Shifts");
            b.HasKey(x => x.Id);
        });
    }
}
