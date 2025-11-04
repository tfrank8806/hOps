using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace hOps.web.Migrations
{
    /// <inheritdoc />
    public partial class AddAnnouncementAndBulletinAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BulletinPostAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BulletinPostId = table.Column<int>(type: "INTEGER", nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    OriginalFileName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BulletinPostAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BulletinPostAttachments_BulletinPosts_BulletinPostId",
                        column: x => x.BulletinPostId,
                        principalTable: "BulletinPosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ManagerAnnouncementAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ManagerAnnouncementId = table.Column<int>(type: "INTEGER", nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    OriginalFileName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManagerAnnouncementAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ManagerAnnouncementAttachments_ManagerAnnouncements_ManagerAnnouncementId",
                        column: x => x.ManagerAnnouncementId,
                        principalTable: "ManagerAnnouncements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BulletinPostAttachments_BulletinPostId",
                table: "BulletinPostAttachments",
                column: "BulletinPostId");

            migrationBuilder.CreateIndex(
                name: "IX_ManagerAnnouncementAttachments_ManagerAnnouncementId",
                table: "ManagerAnnouncementAttachments",
                column: "ManagerAnnouncementId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BulletinPostAttachments");

            migrationBuilder.DropTable(
                name: "ManagerAnnouncementAttachments");
        }
    }
}
