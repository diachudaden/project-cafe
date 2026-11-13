using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChonRieng.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PaymentLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaymentLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Stage = table.Column<byte>(type: "tinyint", nullable: false),
                    Level = table.Column<byte>(type: "tinyint", nullable: false),
                    PaymentRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PaymentCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PaymentTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProviderTxId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    BillId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BillCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(14,0)", nullable: true),
                    Message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Detail = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TraceId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ElapsedMs = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentLogs_BranchId_OccurredAt",
                table: "PaymentLogs",
                columns: new[] { "BranchId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentLogs_Level_OccurredAt",
                table: "PaymentLogs",
                columns: new[] { "Level", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentLogs_OccurredAt",
                table: "PaymentLogs",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentLogs_PaymentCode_OccurredAt",
                table: "PaymentLogs",
                columns: new[] { "PaymentCode", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentLogs_PaymentRequestId",
                table: "PaymentLogs",
                column: "PaymentRequestId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentLogs");
        }
    }
}
