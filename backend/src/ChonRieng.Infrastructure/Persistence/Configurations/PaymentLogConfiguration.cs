using ChonRieng.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChonRieng.Infrastructure.Persistence.Configurations;

internal sealed class PaymentLogConfiguration : IEntityTypeConfiguration<PaymentLog>
{
    public void Configure(EntityTypeBuilder<PaymentLog> builder)
    {
        // Plain trace table: no foreign keys, so a log row never blocks deleting or changing payment data.
        builder.ToTable("PaymentLogs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Stage).HasConversion<byte>();
        builder.Property(x => x.Level).HasConversion<byte>();
        builder.Property(x => x.PaymentCode).HasMaxLength(20);
        builder.Property(x => x.ProviderTxId).HasMaxLength(128);
        builder.Property(x => x.BillCode).HasMaxLength(16);
        builder.Property(x => x.Amount).HasColumnType("decimal(14,0)");
        builder.Property(x => x.Message).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Detail).HasMaxLength(2000);
        builder.Property(x => x.TraceId).HasMaxLength(64);

        builder.HasIndex(x => x.OccurredAt);
        builder.HasIndex(x => new { x.PaymentCode, x.OccurredAt });
        builder.HasIndex(x => x.PaymentRequestId);
        builder.HasIndex(x => new { x.BranchId, x.OccurredAt });
        builder.HasIndex(x => new { x.Level, x.OccurredAt });
    }
}
