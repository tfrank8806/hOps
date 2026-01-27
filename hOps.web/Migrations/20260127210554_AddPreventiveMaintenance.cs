using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace hOps.web.Migrations
{
    /// <inheritdoc />
    public partial class AddPreventiveMaintenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PreventiveMaintenanceSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PropertyId = table.Column<int>(type: "integer", nullable: false),
                    RoomId = table.Column<int>(type: "integer", nullable: true),
                    RoomNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PausedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastResumedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TotalDurationSeconds = table.Column<double>(type: "double precision", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedById = table.Column<string>(type: "text", nullable: false),
                    CompletedById = table.Column<string>(type: "text", nullable: true),
                    LastSavedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreventiveMaintenanceSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PreventiveMaintenanceSessions_AspNetUsers_CompletedById",
                        column: x => x.CompletedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PreventiveMaintenanceSessions_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PreventiveMaintenanceSessions_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PreventiveMaintenanceSessions_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PreventiveMaintenanceSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FrequencyPerYear = table.Column<int>(type: "integer", nullable: false),
                    PropertyId = table.Column<int>(type: "integer", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreventiveMaintenanceSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PreventiveMaintenanceSettings_AspNetUsers_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PreventiveMaintenanceSettings_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PreventiveMaintenanceTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    PropertyId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreventiveMaintenanceTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PreventiveMaintenanceTasks_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PreventiveMaintenanceSessionTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SessionId = table.Column<int>(type: "integer", nullable: false),
                    TemplateTaskId = table.Column<int>(type: "integer", nullable: true),
                    TaskName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TaskDescription = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreventiveMaintenanceSessionTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PreventiveMaintenanceSessionTasks_PreventiveMaintenanceSess~",
                        column: x => x.SessionId,
                        principalTable: "PreventiveMaintenanceSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PreventiveMaintenanceSessionTasks_PreventiveMaintenanceTask~",
                        column: x => x.TemplateTaskId,
                        principalTable: "PreventiveMaintenanceTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PreventiveMaintenanceSessions_CompletedById",
                table: "PreventiveMaintenanceSessions",
                column: "CompletedById");

            migrationBuilder.CreateIndex(
                name: "IX_PreventiveMaintenanceSessions_CreatedById",
                table: "PreventiveMaintenanceSessions",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_PreventiveMaintenanceSessions_PropertyId_Status",
                table: "PreventiveMaintenanceSessions",
                columns: new[] { "PropertyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PreventiveMaintenanceSessions_RoomId",
                table: "PreventiveMaintenanceSessions",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_PreventiveMaintenanceSessionTasks_SessionId_SortOrder",
                table: "PreventiveMaintenanceSessionTasks",
                columns: new[] { "SessionId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_PreventiveMaintenanceSessionTasks_TemplateTaskId",
                table: "PreventiveMaintenanceSessionTasks",
                column: "TemplateTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_PreventiveMaintenanceSettings_PropertyId",
                table: "PreventiveMaintenanceSettings",
                column: "PropertyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PreventiveMaintenanceSettings_UpdatedByUserId",
                table: "PreventiveMaintenanceSettings",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PreventiveMaintenanceTasks_PropertyId_SortOrder",
                table: "PreventiveMaintenanceTasks",
                columns: new[] { "PropertyId", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PreventiveMaintenanceSessionTasks");

            migrationBuilder.DropTable(
                name: "PreventiveMaintenanceSettings");

            migrationBuilder.DropTable(
                name: "PreventiveMaintenanceSessions");

            migrationBuilder.DropTable(
                name: "PreventiveMaintenanceTasks");
        }
    }
}
