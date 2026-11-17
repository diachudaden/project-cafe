using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChonRieng.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NotificationPaymentLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data only (TASK-025): "/pay" never existed in the customer app; old payment notifications open "Tôi".
            migrationBuilder.Sql("UPDATE [Notifications] SET [Link] = N'/me' WHERE [Link] = N'/pay';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Not reversible: the old "/pay" link was broken anyway.
        }
    }
}
