using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChonRieng.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RoleWorkflows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PointTransactions_CustomerId",
                table: "PointTransactions");

            migrationBuilder.DropIndex(
                name: "IX_Bills_BranchId",
                table: "Bills");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "ShiftRequests",
                type: "datetimeoffset",
                nullable: false,
                defaultValueSql: "SYSDATETIMEOFFSET()");

            migrationBuilder.AddColumn<string>(
                name: "ReviewNote",
                table: "ShiftClosings",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReviewedAt",
                table: "ShiftClosings",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewedByUserId",
                table: "ShiftClosings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "Status",
                table: "ShiftClosings",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)1);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SubmittedAt",
                table: "ShiftClosings",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeclineReason",
                table: "ShiftAssignments",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OverrideReason",
                table: "ShiftAssignments",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ClosedAt",
                table: "Sessions",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "PointTransactions",
                type: "datetimeoffset",
                nullable: false,
                defaultValueSql: "SYSDATETIMEOFFSET()");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "Notifications",
                type: "datetimeoffset",
                nullable: false,
                defaultValueSql: "SYSDATETIMEOFFSET()");

            migrationBuilder.AlterColumn<Guid>(
                name: "CreatedByEmployeeId",
                table: "Expenses",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "Expenses",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReversesExpenseId",
                table: "Expenses",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MemberCode",
                table: "Customers",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "Bills",
                type: "datetimeoffset",
                nullable: false,
                defaultValueSql: "SYSDATETIMEOFFSET()");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ApprovedAt",
                table: "Attendances",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedByUserId",
                table: "Attendances",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShiftRequests_Status_ShiftId",
                table: "ShiftRequests",
                columns: new[] { "Status", "ShiftId" });

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_SeatId_Active",
                table: "Sessions",
                column: "SeatId",
                unique: true,
                filter: "[Status] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_PointTransactions_CustomerId_CreatedAt",
                table: "PointTransactions",
                columns: new[] { "CustomerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_CreatedAt",
                table: "Notifications",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_ReversesExpenseId",
                table: "Expenses",
                column: "ReversesExpenseId",
                unique: true,
                filter: "[ReversesExpenseId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_MemberCode",
                table: "Customers",
                column: "MemberCode",
                unique: true,
                filter: "[MemberCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Bills_BranchId_CreatedAt",
                table: "Bills",
                columns: new[] { "BranchId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ShiftRequests_Status_ShiftId",
                table: "ShiftRequests");

            migrationBuilder.DropIndex(
                name: "IX_Sessions_SeatId_Active",
                table: "Sessions");

            migrationBuilder.DropIndex(
                name: "IX_PointTransactions_CustomerId_CreatedAt",
                table: "PointTransactions");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_UserId_CreatedAt",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_ReversesExpenseId",
                table: "Expenses");

            migrationBuilder.DropIndex(
                name: "IX_Customers_MemberCode",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Bills_BranchId_CreatedAt",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "ShiftRequests");

            migrationBuilder.DropColumn(
                name: "ReviewNote",
                table: "ShiftClosings");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "ShiftClosings");

            migrationBuilder.DropColumn(
                name: "ReviewedByUserId",
                table: "ShiftClosings");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "ShiftClosings");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "ShiftClosings");

            migrationBuilder.DropColumn(
                name: "DeclineReason",
                table: "ShiftAssignments");

            migrationBuilder.DropColumn(
                name: "OverrideReason",
                table: "ShiftAssignments");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "PointTransactions");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "ReversesExpenseId",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "MemberCode",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "Attendances");

            migrationBuilder.DropColumn(
                name: "ApprovedByUserId",
                table: "Attendances");

            migrationBuilder.AlterColumn<Guid>(
                name: "CreatedByEmployeeId",
                table: "Expenses",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PointTransactions_CustomerId",
                table: "PointTransactions",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Bills_BranchId",
                table: "Bills",
                column: "BranchId");
        }
    }
}
