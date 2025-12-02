using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace hOps.web.Migrations
{
    /// <inheritdoc />
    public partial class AddUserHomeLayout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserNotifications_PassOnLogs_PassOnLogId",
                table: "UserNotifications");

            migrationBuilder.CreateTable(
                name: "UserHomeLayouts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LayoutJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserHomeLayouts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserHomeLayouts_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserHomeLayouts_UserId",
                table: "UserHomeLayouts",
                column: "UserId",
                unique: true);

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

            migrationBuilder.DropTable(
                name: "UserHomeLayouts");

            migrationBuilder.AddForeignKey(
                name: "FK_UserNotifications_PassOnLogs_PassOnLogId",
                table: "UserNotifications",
                column: "PassOnLogId",
                principalTable: "PassOnLogs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
