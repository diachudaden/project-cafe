using ChonRieng.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChonRieng.Infrastructure.Persistence.Configurations;

internal sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("Employees");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.UserId).IsUnique();
        builder.Property(x => x.HourlyRate).HasColumnType("decimal(12,0)");
        builder.Property(x => x.NightAllowance).HasColumnType("decimal(12,0)");
        builder.Property(x => x.PinHash).HasMaxLength(256).IsRequired();
    }
}

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.UserId).IsUnique();
        builder.Property(x => x.Tier).HasMaxLength(40).IsRequired();
        builder.Property(x => x.TotalSpent).HasColumnType("decimal(12,0)");
        builder.Property(x => x.MemberCode).HasMaxLength(16);
        builder.HasIndex(x => x.MemberCode)
            .IsUnique()
            .HasFilter("[MemberCode] IS NOT NULL");
    }
}

internal sealed class EmployeeBranchConfiguration : IEntityTypeConfiguration<EmployeeBranch>
{
    public void Configure(EntityTypeBuilder<EmployeeBranch> builder)
    {
        builder.ToTable("EmployeeBranches");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.EmployeeId, x.BranchId }).IsUnique();
        builder.HasIndex(x => x.EmployeeId)
            .IsUnique()
            .HasFilter("[IsHome] = 1");
        builder.HasOne(x => x.Employee)
            .WithMany(x => x.Branches)
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Branch)
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class AvailabilityConfiguration : IEntityTypeConfiguration<Availability>
{
    public void Configure(EntityTypeBuilder<Availability> builder)
    {
        builder.ToTable("Availabilities", table =>
        {
            table.HasCheckConstraint(
                "CK_Availabilities_Recurrence",
                "([IsRecurring] = 1 AND [DayOfWeek] IS NOT NULL AND [SpecificDate] IS NULL) OR ([IsRecurring] = 0 AND [DayOfWeek] IS NULL AND [SpecificDate] IS NOT NULL)");
            table.HasCheckConstraint("CK_Availabilities_TimeRange", "[From] < [To]");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DayOfWeek).HasConversion<byte?>();
        builder.HasIndex(x => new { x.EmployeeId, x.DayOfWeek, x.SpecificDate });
        builder.HasOne(x => x.Employee)
            .WithMany()
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PayrollConfiguration : IEntityTypeConfiguration<Payroll>
{
    public void Configure(EntityTypeBuilder<Payroll> builder)
    {
        builder.ToTable("Payrolls");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Month).HasColumnType("date");
        builder.Property(x => x.TotalHours).HasPrecision(8, 2);
        builder.Property(x => x.BaseAmount).HasColumnType("decimal(12,0)");
        builder.Property(x => x.Allowance).HasColumnType("decimal(12,0)");
        builder.Property(x => x.Bonus).HasColumnType("decimal(12,0)");
        builder.Property(x => x.Deduction).HasColumnType("decimal(12,0)");
        builder.Property(x => x.NetAmount).HasColumnType("decimal(12,0)");
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => new { x.EmployeeId, x.Month }).IsUnique();
        builder.HasOne(x => x.Employee)
            .WithMany(x => x.Payrolls)
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
