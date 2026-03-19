using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace hOps.web.Migrations
{
    /// <inheritdoc />
    public partial class AddMprDeepCleanRooms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DeepCleanRooms",
                table: "HousekeepingMprEntries",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "DeepCleanStandardMinutes",
                table: "HousekeepingMprEntries",
                type: "numeric",
                nullable: false,
                defaultValue: 60m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeepCleanRooms",
                table: "HousekeepingMprEntries");

            migrationBuilder.DropColumn(
                name: "DeepCleanStandardMinutes",
                table: "HousekeepingMprEntries");
        }
    }
}
