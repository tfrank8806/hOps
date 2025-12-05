using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace hOps.web.Migrations
{
    public partial class AddLayoutMarketplace : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDefault",
                table: "UserHomeLayouts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PersonaKey",
                table: "UserHomeLayouts",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "default");

            migrationBuilder.CreateTable(
                name: "WidgetMarketplaceModules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WidgetId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WidgetMarketplaceModules", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserHomeLayouts_UserId_PersonaKey",
                table: "UserHomeLayouts",
                columns: new[] { "UserId", "PersonaKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WidgetMarketplaceModules_WidgetId",
                table: "WidgetMarketplaceModules",
                column: "WidgetId",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WidgetMarketplaceModules");

            migrationBuilder.DropIndex(
                name: "IX_UserHomeLayouts_UserId_PersonaKey",
                table: "UserHomeLayouts");

            migrationBuilder.DropColumn(
                name: "IsDefault",
                table: "UserHomeLayouts");

            migrationBuilder.DropColumn(
                name: "PersonaKey",
                table: "UserHomeLayouts");
        }
    }
}
