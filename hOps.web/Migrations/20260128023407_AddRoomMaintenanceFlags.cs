using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace hOps.web.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomMaintenanceFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IncludeInDeepClean",
                table: "Rooms",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IncludeInPreventiveMaintenance",
                table: "Rooms",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            if (migrationBuilder.ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
            {
                migrationBuilder.Sql("UPDATE Rooms SET IncludeInDeepClean = 1, IncludeInPreventiveMaintenance = 1;");
            }
            else
            {
                migrationBuilder.Sql("UPDATE \"Rooms\" SET \"IncludeInDeepClean\" = TRUE, \"IncludeInPreventiveMaintenance\" = TRUE;");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IncludeInDeepClean",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "IncludeInPreventiveMaintenance",
                table: "Rooms");
        }
    }
}
