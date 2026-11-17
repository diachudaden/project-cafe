using ChonRieng.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChonRieng.Infrastructure.Persistence.Configurations;

internal sealed class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.ToTable("Sessions");
        builder.HasKey(x => x.Id);
        // Status and EndAt are concurrency tokens: every tracked write (extend, close, add-on sale, VietQR
        // settlement) only succeeds if the row is still in the state it read, so it cannot overwrite an
        // auto-release (TASK-024) or a concurrent extension.
        builder.Property(x => x.Status).HasConversion<byte>().IsConcurrencyToken();
        builder.Property(x => x.EndAt).IsConcurrencyToken();
        builder.Property(x => x.CloseReason).HasConversion<byte?>();
        builder.HasIndex(x => new { x.SeatId, x.Status });
        // Auto-release worker: active sessions by end time.
        builder.HasIndex(x => new { x.Status, x.EndAt });
        // At most one active session per seat; protects against two POS tablets selling the same seat.
        builder.HasIndex(x => x.SeatId)
            .IsUnique()
            .HasFilter("[Status] = 1")
            .HasDatabaseName("IX_Sessions_SeatId_Active");
        builder.HasOne(x => x.Seat)
            .WithMany(x => x.Sessions)
            .HasForeignKey(x => x.SeatId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Customer)
            .WithMany(x => x.Sessions)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PricePlan)
            .WithMany(x => x.Sessions)
            .HasForeignKey(x => x.PricePlanId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SessionExtensionConfiguration : IEntityTypeConfiguration<SessionExtension>
{
    public void Configure(EntityTypeBuilder<SessionExtension> builder)
    {
        builder.ToTable("SessionExtensions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Price).HasColumnType("decimal(12,0)");
        builder.HasIndex(x => x.SessionId);
        builder.HasOne(x => x.Session)
            .WithMany(x => x.Extensions)
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class BillConfiguration : IEntityTypeConfiguration<Bill>
{
    public void Configure(EntityTypeBuilder<Bill> builder)
    {
        builder.ToTable("Bills");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(8).IsRequired();
        builder.Property(x => x.Total).HasColumnType("decimal(12,0)");
        builder.Property(x => x.CashPaidTotal).HasColumnType("decimal(12,0)");
        builder.Property(x => x.VietQrPaidTotal).HasColumnType("decimal(12,0)");
        builder.Property(x => x.PaymentMethod).HasConversion<byte>();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => x.ClaimedByCustomerId);
        builder.HasIndex(x => new { x.BranchId, x.CreatedAt });
        // A concurrent second claim fails instead of awarding points twice.
        builder.Property(x => x.ClaimedByCustomerId).IsConcurrencyToken();
        builder.HasIndex(x => x.SessionId).IsUnique();
        builder.HasOne(x => x.Branch)
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Shift)
            .WithMany(x => x.Bills)
            .HasForeignKey(x => x.ShiftId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Session)
            .WithOne(x => x.Bill)
            .HasForeignKey<Bill>(x => x.SessionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ClaimedByCustomer)
            .WithMany(x => x.ClaimedBills)
            .HasForeignKey(x => x.ClaimedByCustomerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PointTransactionConfiguration : IEntityTypeConfiguration<PointTransaction>
{
    public void Configure(EntityTypeBuilder<PointTransaction> builder)
    {
        builder.ToTable("PointTransactions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Reason).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => new { x.CustomerId, x.CreatedAt });
        builder.HasOne(x => x.Customer)
            .WithMany(x => x.PointTransactions)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Bill)
            .WithMany(x => x.PointTransactions)
            .HasForeignKey(x => x.BillId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
