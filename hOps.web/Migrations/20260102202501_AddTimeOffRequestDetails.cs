using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace hOps.web.Migrations
{
    /// <inheritdoc />
    public partial class AddTimeOffRequestDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsUnpaid",
                table: "ScheduleTimeOffRequests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "SickTimeHours",
                table: "ScheduleTimeOffRequests",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VacationHours",
                table: "ScheduleTimeOffRequests",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsUnpaid",
                table: "ScheduleTimeOffRequests");

            migrationBuilder.DropColumn(
                name: "SickTimeHours",
                table: "ScheduleTimeOffRequests");

            migrationBuilder.DropColumn(
                name: "VacationHours",
                table: "ScheduleTimeOffRequests");
        }
    }
}
