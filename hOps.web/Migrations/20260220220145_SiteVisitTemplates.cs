using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace hOps.web.Migrations
{
    /// <inheritdoc />
    public partial class SiteVisitTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SiteVisitTemplateId",
                table: "SiteVisitReports",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SiteVisitTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteVisitTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SiteVisitTemplates_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SiteVisitTemplateItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SiteVisitTemplateId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteVisitTemplateItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SiteVisitTemplateItems_SiteVisitTemplates_SiteVisitTemplate~",
                        column: x => x.SiteVisitTemplateId,
                        principalTable: "SiteVisitTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SiteVisitReports_SiteVisitTemplateId",
                table: "SiteVisitReports",
                column: "SiteVisitTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_SiteVisitTemplateItems_SiteVisitTemplateId_SortOrder",
                table: "SiteVisitTemplateItems",
                columns: new[] { "SiteVisitTemplateId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_SiteVisitTemplates_CreatedByUserId",
                table: "SiteVisitTemplates",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SiteVisitTemplates_Name",
                table: "SiteVisitTemplates",
                column: "Name");

            migrationBuilder.AddForeignKey(
                name: "FK_SiteVisitReports_SiteVisitTemplates_SiteVisitTemplateId",
                table: "SiteVisitReports",
                column: "SiteVisitTemplateId",
                principalTable: "SiteVisitTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SiteVisitReports_SiteVisitTemplates_SiteVisitTemplateId",
                table: "SiteVisitReports");

            migrationBuilder.DropTable(
                name: "SiteVisitTemplateItems");

            migrationBuilder.DropTable(
                name: "SiteVisitTemplates");

            migrationBuilder.DropIndex(
                name: "IX_SiteVisitReports_SiteVisitTemplateId",
                table: "SiteVisitReports");

            migrationBuilder.DropColumn(
                name: "SiteVisitTemplateId",
                table: "SiteVisitReports");
        }
    }
}
