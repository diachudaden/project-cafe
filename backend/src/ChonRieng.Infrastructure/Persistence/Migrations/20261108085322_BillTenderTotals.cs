using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChonRieng.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BillTenderTotals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CashPaidTotal",
                table: "Bills",
                type: "decimal(12,0)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "VietQrPaidTotal",
                table: "Bills",
                type: "decimal(12,0)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.Sql("""
                UPDATE [Bills]
                SET [CashPaidTotal] = [Total]
                WHERE [PaymentMethod] = 1;

                UPDATE [Bills]
                SET [VietQrPaidTotal] = [Total]
                WHERE [PaymentMethod] = 2;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CashPaidTotal",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "VietQrPaidTotal",
                table: "Bills");
        }
    }
}
