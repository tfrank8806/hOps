using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace hOps.web.Migrations
{
    /// <inheritdoc />
    public partial class AddBookmarkSections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BookmarkSectionGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookmarkSectionGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookmarkSectionGroups_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BookmarkSectionAssignments",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    BookmarkId = table.Column<int>(type: "integer", nullable: false),
                    SectionGroupId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookmarkSectionAssignments", x => new { x.UserId, x.BookmarkId });
                    table.ForeignKey(
                        name: "FK_BookmarkSectionAssignments_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BookmarkSectionAssignments_BookmarkSectionGroups_SectionGro~",
                        column: x => x.SectionGroupId,
                        principalTable: "BookmarkSectionGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BookmarkSectionAssignments_Bookmarks_BookmarkId",
                        column: x => x.BookmarkId,
                        principalTable: "Bookmarks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BookmarkSectionAssignments_BookmarkId",
                table: "BookmarkSectionAssignments",
                column: "BookmarkId");

            migrationBuilder.CreateIndex(
                name: "IX_BookmarkSectionAssignments_SectionGroupId",
                table: "BookmarkSectionAssignments",
                column: "SectionGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_BookmarkSectionGroups_UserId",
                table: "BookmarkSectionGroups",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookmarkSectionAssignments");

            migrationBuilder.DropTable(
                name: "BookmarkSectionGroups");
        }
    }
}
