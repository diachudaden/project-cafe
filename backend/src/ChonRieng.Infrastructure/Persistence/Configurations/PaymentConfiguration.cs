using ChonRieng.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChonRieng.Infrastructure.Persistence.Configurations;

internal sealed class PaymentRequestConfiguration : IEntityTypeConfiguration<PaymentRequest>
{
    public void Configure(EntityTypeBuilder<PaymentRequest> builder)
    {
        builder.ToTable("PaymentRequests");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Amount).HasColumnType("decimal(12,0)");
        builder.Property(x => x.Status).HasConversion<byte>();
        builder.Property(x => x.Kind).HasConversion<byte>();
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => x.SeatId)
            .HasFilter("[Status] = 1")
            .IsUnique()
            .HasDatabaseName("IX_PaymentRequests_SeatId_Pending");
        builder.HasIndex(x => x.BranchId);
        builder.HasIndex(x => x.CreatedAt);

        builder.HasOne(x => x.Branch)
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Seat)
            .WithMany()
            .HasForeignKey(x => x.SeatId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.PricePlan)
            .WithMany()
            .HasForeignKey(x => x.PricePlanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Customer)
            .WithMany(x => x.PaymentRequests)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Session)
            .WithMany()
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Shift)
            .WithMany()
            .HasForeignKey(x => x.ShiftId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Bill)
            .WithMany()
            .HasForeignKey(x => x.BillId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> builder)
    {
        builder.ToTable("PaymentTransactions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Provider).HasMaxLength(32).IsRequired();
        builder.Property(x => x.ProviderTxId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.AccountNumber).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Code).HasMaxLength(32);
        builder.Property(x => x.Content).HasMaxLength(256);
        builder.Property(x => x.ReferenceCode).HasMaxLength(64);
        builder.Property(x => x.Note).HasMaxLength(512);
        builder.Property(x => x.MatchStatus).HasConversion<byte>();

        builder.HasIndex(x => x.ProviderTxId).IsUnique();
        builder.HasIndex(x => x.PaymentRequestId);

        builder.HasOne(x => x.PaymentRequest)
            .WithMany(x => x.Transactions)
            .HasForeignKey(x => x.PaymentRequestId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
