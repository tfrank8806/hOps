using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace hOps.web.Migrations
{
    /// <inheritdoc />
    public partial class AddPassOnLogNotificationLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PassOnLogId",
                table: "UserNotifications",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserNotifications_PassOnLogId",
                table: "UserNotifications",
                column: "PassOnLogId");

            migrationBuilder.AddForeignKey(
                name: "FK_UserNotifications_PassOnLogs_PassOnLogId",
                table: "UserNotifications",
                column: "PassOnLogId",
                principalTable: "PassOnLogs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserNotifications_PassOnLogs_PassOnLogId",
                table: "UserNotifications");

            migrationBuilder.DropIndex(
                name: "IX_UserNotifications_PassOnLogId",
                table: "UserNotifications");

            migrationBuilder.DropColumn(
                name: "PassOnLogId",
                table: "UserNotifications");
        }
    }
}
