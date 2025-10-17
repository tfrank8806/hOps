using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace hOps.web.Migrations
{
    /// <inheritdoc />
    public partial class AddCalendarEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CalendarEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CalendarCategoryId = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    StartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    EndDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndTime = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    Recurrence = table.Column<int>(type: "INTEGER", nullable: false),
                    Details = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedById = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalendarEvents_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CalendarEvents_CalendarCategories_CalendarCategoryId",
                        column: x => x.CalendarCategoryId,
                        principalTable: "CalendarCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CalendarEventProperties",
                columns: table => new
                {
                    CalendarEventId = table.Column<int>(type: "INTEGER", nullable: false),
                    PropertyId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarEventProperties", x => new { x.CalendarEventId, x.PropertyId });
                    table.ForeignKey(
                        name: "FK_CalendarEventProperties_CalendarEvents_CalendarEventId",
                        column: x => x.CalendarEventId,
                        principalTable: "CalendarEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CalendarEventProperties_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEventProperties_PropertyId",
                table: "CalendarEventProperties",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_CalendarCategoryId",
                table: "CalendarEvents",
                column: "CalendarCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_CreatedById",
                table: "CalendarEvents",
                column: "CreatedById");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CalendarEventProperties");

            migrationBuilder.DropTable(
                name: "CalendarEvents");
        }
    }
}
