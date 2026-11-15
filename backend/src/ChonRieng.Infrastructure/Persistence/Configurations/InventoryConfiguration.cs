using ChonRieng.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChonRieng.Infrastructure.Persistence.Configurations;

internal sealed class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> builder)
    {
        builder.ToTable("Warehouses");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Type).HasConversion<byte>();
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => new { x.BranchId, x.Type }).IsUnique();
        builder.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class InventoryItemConfiguration : IEntityTypeConfiguration<InventoryItem>
{
    public void Configure(EntityTypeBuilder<InventoryItem> builder)
    {
        builder.ToTable("InventoryItems");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.BaseUnit).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Category).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.Name).IsUnique();
    }
}

internal sealed class ItemUnitConfiguration : IEntityTypeConfiguration<ItemUnit>
{
    public void Configure(EntityTypeBuilder<ItemUnit> builder)
    {
        builder.ToTable("ItemUnits", table => table.HasCheckConstraint("CK_ItemUnits_Factor", "[Factor] > 1"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.UnitName).HasMaxLength(30).IsRequired();
        builder.HasIndex(x => new { x.ItemId, x.UnitName }).IsUnique();
        builder.HasOne(x => x.Item).WithMany(x => x.Units).HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ItemBranchSettingConfiguration : IEntityTypeConfiguration<ItemBranchSetting>
{
    public void Configure(EntityTypeBuilder<ItemBranchSetting> builder)
    {
        builder.ToTable("ItemBranchSettings");
        builder.HasKey(x => new { x.ItemId, x.BranchId });
        builder.Property(x => x.NormPerSession).HasColumnType("decimal(8,3)");
        builder.Property(x => x.NormOverride).HasColumnType("decimal(8,3)");
        builder.Ignore(x => x.EffectiveNorm);
        builder.HasOne(x => x.Item).WithMany(x => x.BranchSettings).HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class StockDocumentConfiguration : IEntityTypeConfiguration<StockDocument>
{
    public void Configure(EntityTypeBuilder<StockDocument> builder)
    {
        builder.ToTable("StockDocuments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Type).HasConversion<byte>();
        builder.Property(x => x.Status).HasConversion<byte>();
        builder.Property(x => x.Note).HasMaxLength(1000);
        builder.Property(x => x.Reason).HasMaxLength(1000);
        builder.Property(x => x.ApprovalNote).HasMaxLength(1000);
        builder.Property(x => x.Supplier).HasMaxLength(200);
        builder.Property(x => x.InvoiceNo).HasMaxLength(100);
        builder.Property(x => x.PhotoUrl).HasMaxLength(500);
        builder.Property(x => x.PayloadHash).HasMaxLength(64).IsFixedLength();
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => new { x.BranchId, x.Type, x.CreatedAt });
        builder.HasIndex(x => new { x.BranchId, x.Status });
        // One live shift-close count per (shift, warehouse); a rejected one may be redone.
        builder.HasIndex(x => new { x.ShiftId, x.ToWarehouseId })
            .IsUnique()
            .HasFilter("[Type] = 4 AND [ShiftId] IS NOT NULL AND [Status] <> 5");
        builder.HasIndex(x => x.AdjustsDocumentId);

        builder.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.FromWarehouse).WithMany().HasForeignKey(x => x.FromWarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ToWarehouse).WithMany().HasForeignKey(x => x.ToWarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Shift).WithMany().HasForeignKey(x => x.ShiftId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AdjustsDocument).WithMany().HasForeignKey(x => x.AdjustsDocumentId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class StockDocumentLineConfiguration : IEntityTypeConfiguration<StockDocumentLine>
{
    public void Configure(EntityTypeBuilder<StockDocumentLine> builder)
    {
        builder.ToTable("StockDocumentLines");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.UnitName).HasMaxLength(30);
        builder.Property(x => x.UnitCost).HasColumnType("decimal(12,0)");
        builder.Property(x => x.Norm).HasColumnType("decimal(8,3)");
        builder.Property(x => x.Usage).HasColumnType("decimal(12,3)");
        builder.HasIndex(x => new { x.DocumentId, x.ItemId }).IsUnique();
        builder.HasIndex(x => x.ItemId);
        builder.HasOne(x => x.Document).WithMany(x => x.Lines).HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("StockMovements", table => table.HasCheckConstraint("CK_StockMovements_Qty", "[Qty] <> 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityColumn();
        builder.HasIndex(x => new { x.WarehouseId, x.ItemId, x.OccurredAt });
        builder.HasIndex(x => x.DocumentId);
        builder.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Document).WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class StockBalanceConfiguration : IEntityTypeConfiguration<StockBalance>
{
    public void Configure(EntityTypeBuilder<StockBalance> builder)
    {
        builder.ToTable("StockBalances");
        builder.HasKey(x => new { x.WarehouseId, x.ItemId });
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ShiftCountHandoverConfiguration : IEntityTypeConfiguration<ShiftCountHandover>
{
    public void Configure(EntityTypeBuilder<ShiftCountHandover> builder)
    {
        builder.ToTable("ShiftCountHandovers");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasConversion<byte>();
        builder.Property(x => x.DisputeNote).HasMaxLength(1000);
        builder.Property(x => x.ResolveNote).HasMaxLength(1000);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasIndex(x => x.CountDocumentId).IsUnique();
        builder.HasIndex(x => x.Status);
        builder.HasOne(x => x.CountDocument).WithMany().HasForeignKey(x => x.CountDocumentId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class InventoryIssueConfiguration : IEntityTypeConfiguration<InventoryIssue>
{
    public void Configure(EntityTypeBuilder<InventoryIssue> builder)
    {
        builder.ToTable("InventoryIssues");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Kind).HasConversion<byte>();
        builder.Property(x => x.Status).HasConversion<byte>();
        builder.Property(x => x.Note).HasMaxLength(1000);
        builder.HasIndex(x => new { x.InnerCountDocumentId, x.ItemId }).IsUnique();
        builder.HasIndex(x => new { x.BranchId, x.Status });
        builder.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
