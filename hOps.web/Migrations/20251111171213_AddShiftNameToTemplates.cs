using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace hOps.web.Migrations
{
    /// <inheritdoc />
    public partial class AddShiftNameToTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ShiftName",
                table: "ScheduleShiftTemplates",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE "ScheduleShiftTemplates"
                SET "ShiftName" = COALESCE(NULLIF("ShiftName", ''), "Name")
            """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShiftName",
                table: "ScheduleShiftTemplates");
        }
    }
}
