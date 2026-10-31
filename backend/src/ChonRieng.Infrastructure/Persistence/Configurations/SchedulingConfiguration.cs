using ChonRieng.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChonRieng.Infrastructure.Persistence.Configurations;

internal sealed class ShiftConfiguration : IEntityTypeConfiguration<Shift>
{
    public void Configure(EntityTypeBuilder<Shift> builder)
    {
        builder.ToTable("Shifts", table =>
            table.HasCheckConstraint("CK_Shifts_TimeRange", "[EndAt] > [StartAt]"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Type).HasConversion<byte>();
        builder.Property(x => x.Status).HasConversion<byte>();
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasIndex(x => new { x.BranchId, x.StartAt, x.EndAt });
        builder.HasOne(x => x.Branch)
            .WithMany(x => x.Shifts)
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ShiftAssignmentConfiguration : IEntityTypeConfiguration<ShiftAssignment>
{
    public void Configure(EntityTypeBuilder<ShiftAssignment> builder)
    {
        builder.ToTable("ShiftAssignments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasConversion<byte>();
        builder.HasIndex(x => new { x.ShiftId, x.EmployeeId }).IsUnique();
        builder.HasIndex(x => x.EmployeeId);
        builder.HasOne(x => x.Shift)
            .WithMany(x => x.Assignments)
            .HasForeignKey(x => x.ShiftId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Employee)
            .WithMany(x => x.ShiftAssignments)
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AttendanceConfiguration : IEntityTypeConfiguration<Attendance>
{
    public void Configure(EntityTypeBuilder<Attendance> builder)
    {
        builder.ToTable("Attendances");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Method).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => new { x.EmployeeId, x.ShiftId }).IsUnique();
        builder.HasOne(x => x.Employee)
            .WithMany(x => x.Attendances)
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Shift)
            .WithMany(x => x.Attendances)
            .HasForeignKey(x => x.ShiftId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ShiftClosingConfiguration : IEntityTypeConfiguration<ShiftClosing>
{
    public void Configure(EntityTypeBuilder<ShiftClosing> builder)
    {
        builder.ToTable("ShiftClosings");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.ShiftId).IsUnique();
        builder.Property(x => x.ExpectedCash).HasColumnType("decimal(12,0)");
        builder.Property(x => x.CountedCash).HasColumnType("decimal(12,0)");
        builder.Property(x => x.Difference).HasColumnType("decimal(12,0)");
        builder.Property(x => x.Note).HasMaxLength(1000);
        builder.Property(x => x.Denominations).HasColumnType("nvarchar(max)").IsRequired();
        builder.HasOne(x => x.Shift)
            .WithOne(x => x.Closing)
            .HasForeignKey<ShiftClosing>(x => x.ShiftId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ClosedByEmployee)
            .WithMany()
            .HasForeignKey(x => x.ClosedByEmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ShiftRequestConfiguration : IEntityTypeConfiguration<ShiftRequest>
{
    public void Configure(EntityTypeBuilder<ShiftRequest> builder)
    {
        builder.ToTable("ShiftRequests");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Type).HasConversion<byte>();
        builder.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.HasOne(x => x.Shift)
            .WithMany()
            .HasForeignKey(x => x.ShiftId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.RequesterEmployee)
            .WithMany()
            .HasForeignKey(x => x.RequesterEmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.TargetEmployee)
            .WithMany()
            .HasForeignKey(x => x.TargetEmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ReviewedByEmployee)
            .WithMany()
            .HasForeignKey(x => x.ReviewedByEmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
