using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChonRieng.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InventoryModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "StockDocumentId",
                table: "Expenses",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InventoryItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BaseUnit = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CountDaily = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Warehouses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<byte>(type: "tinyint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Warehouses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Warehouses_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryIssues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PeriodFrom = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PeriodTo = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Kind = table.Column<byte>(type: "tinyint", nullable: false),
                    InnerVar = table.Column<int>(type: "int", nullable: false),
                    OuterVar = table.Column<int>(type: "int", nullable: false),
                    Total = table.Column<int>(type: "int", nullable: false),
                    Tolerance = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    InnerCountDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdjustmentDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ResolvedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryIssues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryIssues_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryIssues_InventoryItems_ItemId",
                        column: x => x.ItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ItemBranchSettings",
                columns: table => new
                {
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NormPerSession = table.Column<decimal>(type: "decimal(8,3)", nullable: false),
                    NormOverride = table.Column<decimal>(type: "decimal(8,3)", nullable: true),
                    OuterMin = table.Column<int>(type: "int", nullable: false),
                    OuterPar = table.Column<int>(type: "int", nullable: false),
                    InnerMin = table.Column<int>(type: "int", nullable: false),
                    NormLearnedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemBranchSettings", x => new { x.ItemId, x.BranchId });
                    table.ForeignKey(
                        name: "FK_ItemBranchSettings_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ItemBranchSettings_InventoryItems_ItemId",
                        column: x => x.ItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItemUnits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitName = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Factor = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemUnits", x => x.Id);
                    table.CheckConstraint("CK_ItemUnits_Factor", "[Factor] > 1");
                    table.ForeignKey(
                        name: "FK_ItemUnits_InventoryItems_ItemId",
                        column: x => x.ItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StockBalances",
                columns: table => new
                {
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Qty = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockBalances", x => new { x.WarehouseId, x.ItemId });
                    table.ForeignKey(
                        name: "FK_StockBalances_InventoryItems_ItemId",
                        column: x => x.ItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockBalances_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Type = table.Column<byte>(type: "tinyint", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromWarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ToWarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ShiftId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Supplier = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    InvoiceNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PhotoUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SessionCount = table.Column<int>(type: "int", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ConfirmedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConfirmedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ApprovedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ApprovalNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AdjustsDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PayloadHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockDocuments_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockDocuments_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "Shifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockDocuments_StockDocuments_AdjustsDocumentId",
                        column: x => x.AdjustsDocumentId,
                        principalTable: "StockDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockDocuments_Warehouses_FromWarehouseId",
                        column: x => x.FromWarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockDocuments_Warehouses_ToWarehouseId",
                        column: x => x.ToWarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ShiftCountHandovers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CountDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReceivedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReceivedShiftId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DisputeNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ResolvedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ResolveNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RemindedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EscalatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftCountHandovers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShiftCountHandovers_StockDocuments_CountDocumentId",
                        column: x => x.CountDocumentId,
                        principalTable: "StockDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockDocumentLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Qty = table.Column<int>(type: "int", nullable: false),
                    UnitName = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    UnitQty = table.Column<int>(type: "int", nullable: true),
                    UnitCost = table.Column<decimal>(type: "decimal(12,0)", nullable: true),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    FirstCountedQty = table.Column<int>(type: "int", nullable: true),
                    CountedQty = table.Column<int>(type: "int", nullable: true),
                    OpeningQty = table.Column<int>(type: "int", nullable: true),
                    TransferInQty = table.Column<int>(type: "int", nullable: true),
                    WasteQty = table.Column<int>(type: "int", nullable: true),
                    Norm = table.Column<decimal>(type: "decimal(8,3)", nullable: true),
                    Usage = table.Column<decimal>(type: "decimal(12,3)", nullable: true),
                    ExpectedQty = table.Column<int>(type: "int", nullable: true),
                    Variance = table.Column<int>(type: "int", nullable: true),
                    Tolerance = table.Column<int>(type: "int", nullable: true),
                    DisputedQty = table.Column<int>(type: "int", nullable: true),
                    ResolvedQty = table.Column<int>(type: "int", nullable: true),
                    SuggestedQty = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockDocumentLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockDocumentLines_InventoryItems_ItemId",
                        column: x => x.ItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockDocumentLines_StockDocuments_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "StockDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockMovements",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Qty = table.Column<int>(type: "int", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockMovements", x => x.Id);
                    table.CheckConstraint("CK_StockMovements_Qty", "[Qty] <> 0");
                    table.ForeignKey(
                        name: "FK_StockMovements_InventoryItems_ItemId",
                        column: x => x.ItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockMovements_StockDocuments_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "StockDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockMovements_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_StockDocumentId",
                table: "Expenses",
                column: "StockDocumentId",
                unique: true,
                filter: "[StockDocumentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryIssues_BranchId_Status",
                table: "InventoryIssues",
                columns: new[] { "BranchId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryIssues_InnerCountDocumentId_ItemId",
                table: "InventoryIssues",
                columns: new[] { "InnerCountDocumentId", "ItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryIssues_ItemId",
                table: "InventoryIssues",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_Name",
                table: "InventoryItems",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ItemBranchSettings_BranchId",
                table: "ItemBranchSettings",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemUnits_ItemId_UnitName",
                table: "ItemUnits",
                columns: new[] { "ItemId", "UnitName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShiftCountHandovers_CountDocumentId",
                table: "ShiftCountHandovers",
                column: "CountDocumentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShiftCountHandovers_Status",
                table: "ShiftCountHandovers",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_StockBalances_ItemId",
                table: "StockBalances",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_StockDocumentLines_DocumentId_ItemId",
                table: "StockDocumentLines",
                columns: new[] { "DocumentId", "ItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockDocumentLines_ItemId",
                table: "StockDocumentLines",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_StockDocuments_AdjustsDocumentId",
                table: "StockDocuments",
                column: "AdjustsDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_StockDocuments_BranchId_Status",
                table: "StockDocuments",
                columns: new[] { "BranchId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_StockDocuments_BranchId_Type_CreatedAt",
                table: "StockDocuments",
                columns: new[] { "BranchId", "Type", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StockDocuments_Code",
                table: "StockDocuments",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockDocuments_FromWarehouseId",
                table: "StockDocuments",
                column: "FromWarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_StockDocuments_ShiftId_ToWarehouseId",
                table: "StockDocuments",
                columns: new[] { "ShiftId", "ToWarehouseId" },
                unique: true,
                filter: "[Type] = 4 AND [ShiftId] IS NOT NULL AND [Status] <> 5");

            migrationBuilder.CreateIndex(
                name: "IX_StockDocuments_ToWarehouseId",
                table: "StockDocuments",
                column: "ToWarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_DocumentId",
                table: "StockMovements",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_ItemId",
                table: "StockMovements",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_WarehouseId_ItemId_OccurredAt",
                table: "StockMovements",
                columns: new[] { "WarehouseId", "ItemId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_BranchId_Type",
                table: "Warehouses",
                columns: new[] { "BranchId", "Type" },
                unique: true);

            // Existing branches get their two warehouses (new branches get them on first use).
            migrationBuilder.Sql(@"
INSERT INTO [Warehouses] ([Id], [BranchId], [Type], [Name])
SELECT NEWID(), b.[Id], t.[Type], t.[Name]
FROM [Branches] b
CROSS JOIN (VALUES (CAST(1 AS tinyint), N'Kho trong'), (CAST(2 AS tinyint), N'Kho ngoài')) AS t([Type], [Name])
WHERE NOT EXISTS (SELECT 1 FROM [Warehouses] w WHERE w.[BranchId] = b.[Id] AND w.[Type] = t.[Type]);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InventoryIssues");

            migrationBuilder.DropTable(
                name: "ItemBranchSettings");

            migrationBuilder.DropTable(
                name: "ItemUnits");

            migrationBuilder.DropTable(
                name: "ShiftCountHandovers");

            migrationBuilder.DropTable(
                name: "StockBalances");

            migrationBuilder.DropTable(
                name: "StockDocumentLines");

            migrationBuilder.DropTable(
                name: "StockMovements");

            migrationBuilder.DropTable(
                name: "InventoryItems");

            migrationBuilder.DropTable(
                name: "StockDocuments");

            migrationBuilder.DropTable(
                name: "Warehouses");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_StockDocumentId",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "StockDocumentId",
                table: "Expenses");
        }
    }
}
