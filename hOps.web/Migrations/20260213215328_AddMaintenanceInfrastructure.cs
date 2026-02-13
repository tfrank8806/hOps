using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace hOps.web.Migrations
{
    /// <inheritdoc />
    public partial class AddMaintenanceInfrastructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EquipmentItemId",
                table: "WorkOrders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ChecklistId",
                table: "PreventiveMaintenanceTasks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AreaLabel",
                table: "PreventiveMaintenanceSessions",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ChecklistId",
                table: "PreventiveMaintenanceSessions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EquipmentItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PropertyId = table.Column<int>(type: "integer", nullable: false),
                    Category = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: true),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Location = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Brand = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Model = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    SerialNumber = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    VendorName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    VendorPhone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    VendorEmail = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    InstalledOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WarrantyEndsOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipmentItems_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceLogTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ColumnsJson = table.Column<string>(type: "text", nullable: false),
                    ScheduleType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    WeeklyDaysBitmask = table.Column<int>(type: "integer", nullable: false),
                    DayOfMonth = table.Column<int>(type: "integer", nullable: true),
                    DueTimeLocal = table.Column<TimeSpan>(type: "interval", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    PropertyId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceLogTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceLogTemplates_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PreventiveMaintenanceChecklists",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ChecklistType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AreaOptionsJson = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    PropertyId = table.Column<int>(type: "integer", nullable: false),
                    CreatedById = table.Column<string>(type: "text", nullable: true),
                    UpdatedById = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreventiveMaintenanceChecklists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PreventiveMaintenanceChecklists_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PreventiveMaintenanceChecklists_AspNetUsers_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PreventiveMaintenanceChecklists_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceLogEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TemplateId = table.Column<int>(type: "integer", nullable: false),
                    EntryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ValuesJson = table.Column<string>(type: "text", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceLogEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceLogEntries_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceLogEntries_MaintenanceLogTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "MaintenanceLogTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                WITH property_scope AS (
                    SELECT DISTINCT "PropertyId" FROM "PreventiveMaintenanceTasks"
                    UNION
                    SELECT DISTINCT "PropertyId" FROM "PreventiveMaintenanceSessions"
                )
                INSERT INTO "PreventiveMaintenanceChecklists"
                ("Name", "ChecklistType", "AreaOptionsJson", "IsActive", "PropertyId", "CreatedAtUtc", "UpdatedAtUtc")
                SELECT 'Room PM Checklist', 'Room', '[]', TRUE, ps."PropertyId", CURRENT_TIMESTAMP, CURRENT_TIMESTAMP
                FROM property_scope ps
                ORDER BY ps."PropertyId";
            """);

            migrationBuilder.Sql("""
                UPDATE "PreventiveMaintenanceTasks"
                SET "ChecklistId" = (
                    SELECT c."Id"
                    FROM "PreventiveMaintenanceChecklists" c
                    WHERE c."PropertyId" = "PreventiveMaintenanceTasks"."PropertyId"
                    ORDER BY c."Id"
                    LIMIT 1
                )
                WHERE "ChecklistId" IS NULL;
            """);

            migrationBuilder.Sql("""
                UPDATE "PreventiveMaintenanceSessions"
                SET "ChecklistId" = (
                    SELECT c."Id"
                    FROM "PreventiveMaintenanceChecklists" c
                    WHERE c."PropertyId" = "PreventiveMaintenanceSessions"."PropertyId"
                    ORDER BY c."Id"
                    LIMIT 1
                )
                WHERE "ChecklistId" IS NULL;
            """);

            migrationBuilder.Sql("""
                UPDATE "PreventiveMaintenanceTasks"
                SET "ChecklistId" = (
                    SELECT MIN("Id") FROM "PreventiveMaintenanceChecklists"
                )
                WHERE "ChecklistId" IS NULL
                  AND EXISTS (SELECT 1 FROM "PreventiveMaintenanceChecklists");
            """);

            migrationBuilder.Sql("""
                UPDATE "PreventiveMaintenanceSessions"
                SET "ChecklistId" = (
                    SELECT MIN("Id") FROM "PreventiveMaintenanceChecklists"
                )
                WHERE "ChecklistId" IS NULL
                  AND EXISTS (SELECT 1 FROM "PreventiveMaintenanceChecklists");
            """);

            migrationBuilder.AlterColumn<int>(
                name: "ChecklistId",
                table: "PreventiveMaintenanceTasks",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ChecklistId",
                table: "PreventiveMaintenanceSessions",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_EquipmentItemId",
                table: "WorkOrders",
                column: "EquipmentItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PreventiveMaintenanceTasks_ChecklistId_SortOrder",
                table: "PreventiveMaintenanceTasks",
                columns: new[] { "ChecklistId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_PreventiveMaintenanceSessions_ChecklistId",
                table: "PreventiveMaintenanceSessions",
                column: "ChecklistId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentItems_PropertyId_Name",
                table: "EquipmentItems",
                columns: new[] { "PropertyId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceLogEntries_CreatedByUserId",
                table: "MaintenanceLogEntries",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceLogEntries_TemplateId_EntryDate",
                table: "MaintenanceLogEntries",
                columns: new[] { "TemplateId", "EntryDate" });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceLogTemplates_PropertyId_IsActive",
                table: "MaintenanceLogTemplates",
                columns: new[] { "PropertyId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_PreventiveMaintenanceChecklists_CreatedById",
                table: "PreventiveMaintenanceChecklists",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_PreventiveMaintenanceChecklists_PropertyId_IsActive",
                table: "PreventiveMaintenanceChecklists",
                columns: new[] { "PropertyId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_PreventiveMaintenanceChecklists_UpdatedById",
                table: "PreventiveMaintenanceChecklists",
                column: "UpdatedById");

            migrationBuilder.AddForeignKey(
                name: "FK_PreventiveMaintenanceSessions_PreventiveMaintenanceChecklis~",
                table: "PreventiveMaintenanceSessions",
                column: "ChecklistId",
                principalTable: "PreventiveMaintenanceChecklists",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PreventiveMaintenanceTasks_PreventiveMaintenanceChecklists_~",
                table: "PreventiveMaintenanceTasks",
                column: "ChecklistId",
                principalTable: "PreventiveMaintenanceChecklists",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkOrders_EquipmentItems_EquipmentItemId",
                table: "WorkOrders",
                column: "EquipmentItemId",
                principalTable: "EquipmentItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PreventiveMaintenanceSessions_PreventiveMaintenanceChecklis~",
                table: "PreventiveMaintenanceSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_PreventiveMaintenanceTasks_PreventiveMaintenanceChecklists_~",
                table: "PreventiveMaintenanceTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkOrders_EquipmentItems_EquipmentItemId",
                table: "WorkOrders");

            migrationBuilder.DropTable(
                name: "EquipmentItems");

            migrationBuilder.DropTable(
                name: "MaintenanceLogEntries");

            migrationBuilder.DropTable(
                name: "PreventiveMaintenanceChecklists");

            migrationBuilder.DropTable(
                name: "MaintenanceLogTemplates");

            migrationBuilder.DropIndex(
                name: "IX_WorkOrders_EquipmentItemId",
                table: "WorkOrders");

            migrationBuilder.DropIndex(
                name: "IX_PreventiveMaintenanceTasks_ChecklistId_SortOrder",
                table: "PreventiveMaintenanceTasks");

            migrationBuilder.DropIndex(
                name: "IX_PreventiveMaintenanceSessions_ChecklistId",
                table: "PreventiveMaintenanceSessions");

            migrationBuilder.DropColumn(
                name: "EquipmentItemId",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "ChecklistId",
                table: "PreventiveMaintenanceTasks");

            migrationBuilder.DropColumn(
                name: "AreaLabel",
                table: "PreventiveMaintenanceSessions");

            migrationBuilder.DropColumn(
                name: "ChecklistId",
                table: "PreventiveMaintenanceSessions");
        }
    }
}
