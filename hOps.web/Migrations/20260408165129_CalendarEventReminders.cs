using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace hOps.web.Migrations
{
    /// <inheritdoc />
    public partial class CalendarEventReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "NotifyAllDepartments",
                table: "CalendarEvents",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "TargetDepartmentId",
                table: "CalendarEvents",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CalendarEventReminders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CalendarEventId = table.Column<int>(type: "integer", nullable: false),
                    ReminderType = table.Column<int>(type: "integer", nullable: false),
                    OccurrenceStartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ScheduledSendUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsSent = table.Column<bool>(type: "boolean", nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarEventReminders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalendarEventReminders_CalendarEvents_CalendarEventId",
                        column: x => x.CalendarEventId,
                        principalTable: "CalendarEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_TargetDepartmentId",
                table: "CalendarEvents",
                column: "TargetDepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEventReminders_CalendarEventId_OccurrenceStartUtc_R~",
                table: "CalendarEventReminders",
                columns: new[] { "CalendarEventId", "OccurrenceStartUtc", "ReminderType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEventReminders_IsSent_ScheduledSendUtc",
                table: "CalendarEventReminders",
                columns: new[] { "IsSent", "ScheduledSendUtc" });

            migrationBuilder.AddForeignKey(
                name: "FK_CalendarEvents_Departments_TargetDepartmentId",
                table: "CalendarEvents",
                column: "TargetDepartmentId",
                principalTable: "Departments",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CalendarEvents_Departments_TargetDepartmentId",
                table: "CalendarEvents");

            migrationBuilder.DropTable(
                name: "CalendarEventReminders");

            migrationBuilder.DropIndex(
                name: "IX_CalendarEvents_TargetDepartmentId",
                table: "CalendarEvents");

            migrationBuilder.DropColumn(
                name: "NotifyAllDepartments",
                table: "CalendarEvents");

            migrationBuilder.DropColumn(
                name: "TargetDepartmentId",
                table: "CalendarEvents");
        }
    }
}
