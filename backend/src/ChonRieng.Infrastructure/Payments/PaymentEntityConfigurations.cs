using ChonRieng.Domain.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChonRieng.Infrastructure.Payments;

// INTEGRATE: nếu AppDbContext.OnModelCreating đã có
//   modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
// thì 3 class dưới tự được nạp. Nếu chưa, thêm:
//   modelBuilder.ApplyConfiguration(new PaymentTransactionConfiguration());
//   modelBuilder.ApplyConfiguration(new LedgerEntryConfiguration());
//   modelBuilder.ApplyConfiguration(new ReconciliationIssueConfiguration());
//
// INTEGRATE: cấu hình Seat (file SeatConfiguration của dự án) cần thêm:
//   b.Property(s => s.RowVersion).IsRowVersion();
//   b.HasIndex(s => s.HeldForBillId);
//   b.HasIndex(s => new { s.Status, s.HeldUntil });

public sealed class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> b)
    {
        b.ToTable("PaymentTransactions");

        // Khoá chính = id SePay → chống trùng ở tầng DB, kể cả 2 webhook đến song song.
        b.HasKey(x => x.SepayId);
        b.Property(x => x.SepayId).ValueGeneratedNever();

        b.Property(x => x.Gateway).HasMaxLength(50).IsRequired();
        b.Property(x => x.AccountNumber).HasMaxLength(50).IsRequired();
        b.Property(x => x.SubAccount).HasMaxLength(50);
        b.Property(x => x.Code).HasMaxLength(50);
        b.Property(x => x.Content).HasMaxLength(500).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.ReferenceCode).HasMaxLength(100);
        b.Property(x => x.Note).HasMaxLength(500);
        b.Property(x => x.RawPayload).HasColumnType("nvarchar(max)").IsRequired();
        b.Property(x => x.MatchStatus).HasConversion<byte>();

        b.Ignore(x => x.NeedsAttention);

        b.HasIndex(x => x.BillId);
        b.HasIndex(x => new { x.MatchStatus, x.ResolvedAtUtc });
        b.HasIndex(x => x.TransactionAtUtc);

        // INTEGRATE: muốn có FK thật tới Bills thì bỏ comment (thay Bill bằng entity của dự án):
        // b.HasOne<Bill>().WithMany().HasForeignKey(x => x.BillId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class LedgerEntryConfiguration : IEntityTypeConfiguration<LedgerEntry>
{
    public void Configure(EntityTypeBuilder<LedgerEntry> b)
    {
        b.ToTable("LedgerEntries", t =>
        {
            t.HasCheckConstraint("CK_LedgerEntries_Amount_Positive", "[Amount] > 0");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).UseIdentityColumn();

        b.Property(x => x.Direction).HasConversion<byte>();
        b.Property(x => x.Category).HasConversion<byte>();
        b.Property(x => x.Method).HasConversion<byte>();
        b.Property(x => x.Note).HasMaxLength(255);

        b.Ignore(x => x.IsReversal);
        b.Ignore(x => x.SignedAmount);

        b.HasIndex(x => new { x.BranchId, x.OccurredAtUtc });
        b.HasIndex(x => new { x.Method, x.Direction, x.OccurredAtUtc });
        b.HasIndex(x => x.BillId);
        b.HasIndex(x => x.ShiftId);

        // Một dòng chỉ được đảo một lần.
        b.HasIndex(x => x.ReversalOfId).IsUnique().HasFilter("[ReversalOfId] IS NOT NULL");

        b.HasOne<LedgerEntry>().WithMany().HasForeignKey(x => x.ReversalOfId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PaymentTransaction>().WithMany().HasForeignKey(x => x.PaymentTransactionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ReconciliationIssueConfiguration : IEntityTypeConfiguration<ReconciliationIssue>
{
    public void Configure(EntityTypeBuilder<ReconciliationIssue> b)
    {
        b.ToTable("ReconciliationIssues");
        b.HasKey(x => x.Id);
        b.Property(x => x.Kind).HasConversion<byte>();
        b.Property(x => x.Message).HasMaxLength(500).IsRequired();
        b.HasIndex(x => new { x.BusinessDate, x.ResolvedAtUtc });
    }
}
