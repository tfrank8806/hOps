using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace hOps.web.Migrations
{
    /// <inheritdoc />
    public partial class AddLocalizationSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PreferredLanguage",
                table: "AspNetUsers",
                type: "text",
                nullable: false,
                defaultValue: "en");

            migrationBuilder.Sql("""
                UPDATE "AspNetUsers"
                SET "PreferredLanguage" = 'en'
                WHERE "PreferredLanguage" IS NULL OR "PreferredLanguage" = '';
                """);

            migrationBuilder.CreateTable(
                name: "TranslatedTexts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Field = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceLanguage = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    TargetLanguage = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    SourceTextHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SourceText = table.Column<string>(type: "text", nullable: false),
                    TranslatedTextValue = table.Column<string>(type: "TEXT", nullable: false),
                    LastUpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TranslatedTexts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TranslatedText_Hash",
                table: "TranslatedTexts",
                columns: new[] { "SourceTextHash", "TargetLanguage" });

            migrationBuilder.CreateIndex(
                name: "IX_TranslatedText_Lookup",
                table: "TranslatedTexts",
                columns: new[] { "EntityType", "EntityId", "Field", "TargetLanguage" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TranslatedTexts");

            migrationBuilder.DropColumn(
                name: "PreferredLanguage",
                table: "AspNetUsers");
        }
    }
}
