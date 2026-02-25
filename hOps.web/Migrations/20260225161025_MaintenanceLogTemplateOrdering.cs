using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace hOps.web.Migrations
{
    /// <inheritdoc />
    public partial class MaintenanceLogTemplateOrdering : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DisplayOrder",
                table: "MaintenanceLogTemplates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(@"
                WITH ranked AS (
                    SELECT ""Id"",
                           ROW_NUMBER() OVER (PARTITION BY ""PropertyId"" ORDER BY ""IsActive"" DESC, ""Name"", ""Id"") - 1 AS rn
                    FROM ""MaintenanceLogTemplates""
                )
                UPDATE ""MaintenanceLogTemplates"" AS t
                SET ""DisplayOrder"" = ranked.rn
                FROM ranked
                WHERE ranked.""Id"" = t.""Id"";
            ");

            migrationBuilder.CreateTable(
                name: "DataProtectionKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FriendlyName = table.Column<string>(type: "text", nullable: true),
                    Xml = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataProtectionKeys", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DataProtectionKeys");

            migrationBuilder.DropColumn(
                name: "DisplayOrder",
                table: "MaintenanceLogTemplates");
        }
    }
}
