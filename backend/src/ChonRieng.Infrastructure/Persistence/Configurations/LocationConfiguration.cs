using ChonRieng.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChonRieng.Infrastructure.Persistence.Configurations;

internal sealed class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.ToTable("Branches");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Address).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Latitude).HasColumnName("Lat");
        builder.Property(x => x.Longitude).HasColumnName("Lng");
        builder.Property(x => x.Latitude).HasPrecision(9, 6);
        builder.Property(x => x.Longitude).HasPrecision(9, 6);
        builder.Property(x => x.Phone).HasMaxLength(32);
    }
}

internal sealed class ZoneConfiguration : IEntityTypeConfiguration<Zone>
{
    public void Configure(EntityTypeBuilder<Zone> builder)
    {
        builder.ToTable("Zones");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => new { x.BranchId, x.Name }).IsUnique();
        builder.HasOne(x => x.Branch)
            .WithMany(x => x.Zones)
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SeatConfiguration : IEntityTypeConfiguration<Seat>
{
    public void Configure(EntityTypeBuilder<Seat> builder)
    {
        builder.ToTable("Seats");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Type).HasConversion<byte>();
        builder.Property(x => x.Status).HasConversion<byte>();
        builder.HasIndex(x => new { x.ZoneId, x.Code }).IsUnique();
        builder.HasOne(x => x.Zone)
            .WithMany(x => x.Seats)
            .HasForeignKey(x => x.ZoneId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PricePlanConfiguration : IEntityTypeConfiguration<PricePlan>
{
    public void Configure(EntityTypeBuilder<PricePlan> builder)
    {
        builder.ToTable("PricePlans");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Price).HasColumnType("decimal(12,0)");
        builder.Property(x => x.ExtendPrice).HasColumnType("decimal(12,0)");
        builder.HasOne(x => x.Branch)
            .WithMany(x => x.PricePlans)
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
