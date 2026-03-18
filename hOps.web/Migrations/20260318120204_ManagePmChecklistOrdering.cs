using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace hOps.web.Migrations
{
    /// <inheritdoc />
    public partial class ManagePmChecklistOrdering : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "PreventiveMaintenanceChecklists",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                WITH ordered AS (
                    SELECT "Id",
                           "PropertyId",
                           ROW_NUMBER() OVER (
                               PARTITION BY "PropertyId"
                               ORDER BY "IsActive" DESC, "Name"
                           ) - 1 AS rn
                    FROM "PreventiveMaintenanceChecklists"
                )
                UPDATE "PreventiveMaintenanceChecklists" AS c
                SET "SortOrder" = ordered.rn
                FROM ordered
                WHERE c."Id" = ordered."Id";
            """);

            migrationBuilder.CreateIndex(
                name: "IX_PreventiveMaintenanceChecklists_PropertyId_SortOrder",
                table: "PreventiveMaintenanceChecklists",
                columns: new[] { "PropertyId", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PreventiveMaintenanceChecklists_PropertyId_SortOrder",
                table: "PreventiveMaintenanceChecklists");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "PreventiveMaintenanceChecklists");
        }
    }
}
