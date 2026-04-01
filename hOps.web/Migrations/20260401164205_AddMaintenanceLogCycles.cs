using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace hOps.web.Migrations
{
    /// <inheritdoc />
    public partial class AddMaintenanceLogCycles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChecklistFileName",
                table: "MaintenanceLogTemplates",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ChecklistFileSizeBytes",
                table: "MaintenanceLogTemplates",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MaintenanceLogCycleCompletions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TemplateId = table.Column<int>(type: "integer", nullable: false),
                    CycleWindowKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ScheduleType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CycleStartLocal = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CycleEndLocal = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CycleDueLocal = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Result = table.Column<int>(type: "integer", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceLogCycleCompletions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceLogCycleCompletions_AspNetUsers_CompletedByUserId",
                        column: x => x.CompletedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MaintenanceLogCycleCompletions_MaintenanceLogTemplates_Temp~",
                        column: x => x.TemplateId,
                        principalTable: "MaintenanceLogTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceLogCompletionAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompletionId = table.Column<int>(type: "integer", nullable: false),
                    FilePath = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    ContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceLogCompletionAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceLogCompletionAttachments_MaintenanceLogCycleComp~",
                        column: x => x.CompletionId,
                        principalTable: "MaintenanceLogCycleCompletions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceLogCompletionAttachments_CompletionId",
                table: "MaintenanceLogCompletionAttachments",
                column: "CompletionId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceLogCycleCompletions_CompletedByUserId",
                table: "MaintenanceLogCycleCompletions",
                column: "CompletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceLogCycleCompletions_TemplateId_CycleWindowKey",
                table: "MaintenanceLogCycleCompletions",
                columns: new[] { "TemplateId", "CycleWindowKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MaintenanceLogCompletionAttachments");

            migrationBuilder.DropTable(
                name: "MaintenanceLogCycleCompletions");

            migrationBuilder.DropColumn(
                name: "ChecklistFileName",
                table: "MaintenanceLogTemplates");

            migrationBuilder.DropColumn(
                name: "ChecklistFileSizeBytes",
                table: "MaintenanceLogTemplates");
        }
    }
}
