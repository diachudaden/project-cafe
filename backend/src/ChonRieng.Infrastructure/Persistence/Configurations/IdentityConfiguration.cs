using ChonRieng.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChonRieng.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.Property(x => x.FullName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.PhoneNumber).HasMaxLength(32);
        builder.Property(x => x.Email).HasMaxLength(256);
        builder.Property(x => x.UserName).HasMaxLength(256);
        builder.Property(x => x.NormalizedEmail).HasMaxLength(256);
        builder.Property(x => x.NormalizedUserName).HasMaxLength(256);

        builder.HasIndex(x => x.NormalizedEmail)
            .IsUnique()
            .HasFilter("[NormalizedEmail] IS NOT NULL");
        builder.HasIndex(x => x.PhoneNumber)
            .IsUnique()
            .HasFilter("[PhoneNumber] IS NOT NULL");

        builder.HasOne(x => x.Employee)
            .WithOne(x => x.User)
            .HasForeignKey<Employee>(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Customer)
            .WithOne(x => x.User)
            .HasForeignKey<Customer>(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
