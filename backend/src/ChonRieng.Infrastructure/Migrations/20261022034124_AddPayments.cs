using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChonRieng.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaymentTransactions",
                columns: table => new
                {
                    SepayId = table.Column<long>(type: "bigint", nullable: false),
                    Gateway = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AccountNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SubAccount = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Content = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReferenceCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    TransactionAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MatchStatus = table.Column<byte>(type: "tinyint", nullable: false),
                    BillId = table.Column<int>(type: "int", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ResolvedBy = table.Column<int>(type: "int", nullable: true),
                    ResolvedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RawPayload = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentTransactions", x => x.SepayId);
                });

            migrationBuilder.CreateTable(
                name: "ReconciliationIssues",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Kind = table.Column<byte>(type: "tinyint", nullable: false),
                    PaymentTransactionId = table.Column<long>(type: "bigint", nullable: true),
                    LedgerTotal = table.Column<long>(type: "bigint", nullable: false),
                    BankTotal = table.Column<long>(type: "bigint", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResolvedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReconciliationIssues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Seats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SeatType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    HeldForBillId = table.Column<int>(type: "int", nullable: true),
                    HeldUntil = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Seats", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SeatId = table.Column<int>(type: "int", nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: true),
                    PricingPlanId = table.Column<int>(type: "int", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Shifts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    OpenedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shifts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LedgerEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Direction = table.Column<byte>(type: "tinyint", nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    Category = table.Column<byte>(type: "tinyint", nullable: false),
                    Method = table.Column<byte>(type: "tinyint", nullable: false),
                    BillId = table.Column<int>(type: "int", nullable: true),
                    PaymentTransactionId = table.Column<long>(type: "bigint", nullable: true),
                    ShiftId = table.Column<int>(type: "int", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ReversalOfId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LedgerEntries", x => x.Id);
                    table.CheckConstraint("CK_LedgerEntries_Amount_Positive", "[Amount] > 0");
                    table.ForeignKey(
                        name: "FK_LedgerEntries_LedgerEntries_ReversalOfId",
                        column: x => x.ReversalOfId,
                        principalTable: "LedgerEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LedgerEntries_PaymentTransactions_PaymentTransactionId",
                        column: x => x.PaymentTransactionId,
                        principalTable: "PaymentTransactions",
                        principalColumn: "SepayId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Bills",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: true),
                    PricingPlanId = table.Column<int>(type: "int", nullable: true),
                    Total = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    IsSeatBill = table.Column<bool>(type: "bit", nullable: false),
                    RequestedSeatId = table.Column<int>(type: "int", nullable: true),
                    RequestedSeatType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PaymentMethod = table.Column<byte>(type: "tinyint", nullable: true),
                    AmountReceived = table.Column<long>(type: "bigint", nullable: true),
                    PaidAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SessionId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bills_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bills_SessionId",
                table: "Bills",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_BillId",
                table: "LedgerEntries",
                column: "BillId");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_BranchId_OccurredAtUtc",
                table: "LedgerEntries",
                columns: new[] { "BranchId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_Method_Direction_OccurredAtUtc",
                table: "LedgerEntries",
                columns: new[] { "Method", "Direction", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_PaymentTransactionId",
                table: "LedgerEntries",
                column: "PaymentTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_ReversalOfId",
                table: "LedgerEntries",
                column: "ReversalOfId",
                unique: true,
                filter: "[ReversalOfId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_ShiftId",
                table: "LedgerEntries",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_BillId",
                table: "PaymentTransactions",
                column: "BillId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_MatchStatus_ResolvedAtUtc",
                table: "PaymentTransactions",
                columns: new[] { "MatchStatus", "ResolvedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_TransactionAtUtc",
                table: "PaymentTransactions",
                column: "TransactionAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationIssues_BusinessDate_ResolvedAtUtc",
                table: "ReconciliationIssues",
                columns: new[] { "BusinessDate", "ResolvedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Seats_HeldForBillId",
                table: "Seats",
                column: "HeldForBillId");

            migrationBuilder.CreateIndex(
                name: "IX_Seats_Status_HeldUntil",
                table: "Seats",
                columns: new[] { "Status", "HeldUntil" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Bills");

            migrationBuilder.DropTable(
                name: "LedgerEntries");

            migrationBuilder.DropTable(
                name: "ReconciliationIssues");

            migrationBuilder.DropTable(
                name: "Seats");

            migrationBuilder.DropTable(
                name: "Shifts");

            migrationBuilder.DropTable(
                name: "Sessions");

            migrationBuilder.DropTable(
                name: "PaymentTransactions");
        }
    }
}
