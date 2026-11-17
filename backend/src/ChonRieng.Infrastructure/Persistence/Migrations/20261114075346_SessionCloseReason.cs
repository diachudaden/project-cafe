using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChonRieng.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SessionCloseReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "CloseReason",
                table: "Sessions",
                type: "tinyint",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClosedByUserId",
                table: "Sessions",
                type: "uniqueidentifier",
                nullable: true);

            // Every session closed before this migration was closed by staff ("Trả chỗ").
            migrationBuilder.Sql("UPDATE [Sessions] SET [CloseReason] = 1 WHERE [Status] <> 1 AND [CloseReason] IS NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_Status_EndAt",
                table: "Sessions",
                columns: new[] { "Status", "EndAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sessions_Status_EndAt",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "CloseReason",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "ClosedByUserId",
                table: "Sessions");
        }
    }
}
