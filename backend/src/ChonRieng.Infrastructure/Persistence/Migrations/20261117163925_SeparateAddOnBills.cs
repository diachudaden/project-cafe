using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChonRieng.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeparateAddOnBills : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Bills_SessionId",
                table: "Bills");

            migrationBuilder.AlterColumn<Guid>(
                name: "SessionId",
                table: "Bills",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<byte>(
                name: "Kind",
                table: "Bills",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)1);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentBillId",
                table: "Bills",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bills_ParentBillId",
                table: "Bills",
                column: "ParentBillId");

            migrationBuilder.CreateIndex(
                name: "IX_Bills_SessionId",
                table: "Bills",
                column: "SessionId",
                unique: true,
                filter: "[SessionId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Bills_Kind",
                table: "Bills",
                sql: "([Kind] = 1 AND [SessionId] IS NOT NULL AND [ParentBillId] IS NULL) OR ([Kind] = 2 AND [SessionId] IS NULL AND [ParentBillId] IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_Bills_Bills_ParentBillId",
                table: "Bills",
                column: "ParentBillId",
                principalTable: "Bills",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bills_Bills_ParentBillId",
                table: "Bills");

            migrationBuilder.DropIndex(
                name: "IX_Bills_ParentBillId",
                table: "Bills");

            migrationBuilder.DropIndex(
                name: "IX_Bills_SessionId",
                table: "Bills");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Bills_Kind",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "ParentBillId",
                table: "Bills");

            migrationBuilder.AlterColumn<Guid>(
                name: "SessionId",
                table: "Bills",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bills_SessionId",
                table: "Bills",
                column: "SessionId",
                unique: true);
        }
    }
}
