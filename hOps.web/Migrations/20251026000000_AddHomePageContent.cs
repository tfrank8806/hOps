using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace hOps.web.Migrations
{
    /// <inheritdoc />
    public partial class AddHomePageContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ManagerAnnouncements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PropertyId = table.Column<int>(type: "INTEGER", nullable: false),
                    Content = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedById = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManagerAnnouncements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ManagerAnnouncements_AspNetUsers_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ManagerAnnouncements_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BulletinPosts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PropertyId = table.Column<int>(type: "INTEGER", nullable: false),
                    Content = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedById = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedById = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BulletinPosts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BulletinPosts_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BulletinPosts_AspNetUsers_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BulletinPosts_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PackageLogEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PropertyId = table.Column<int>(type: "INTEGER", nullable: false),
                    RecipientName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    RoomNumber = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Carrier = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    TrackingNumber = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    LoggedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LoggedById = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackageLogEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PackageLogEntries_AspNetUsers_LoggedById",
                        column: x => x.LoggedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PackageLogEntries_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BulletinPosts_CreatedById",
                table: "BulletinPosts",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_BulletinPosts_PropertyId",
                table: "BulletinPosts",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_BulletinPosts_UpdatedById",
                table: "BulletinPosts",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_ManagerAnnouncements_PropertyId",
                table: "ManagerAnnouncements",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_ManagerAnnouncements_UpdatedById",
                table: "ManagerAnnouncements",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_PackageLogEntries_LoggedById",
                table: "PackageLogEntries",
                column: "LoggedById");

            migrationBuilder.CreateIndex(
                name: "IX_PackageLogEntries_PropertyId",
                table: "PackageLogEntries",
                column: "PropertyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BulletinPosts");

            migrationBuilder.DropTable(
                name: "ManagerAnnouncements");

            migrationBuilder.DropTable(
                name: "PackageLogEntries");
        }
    }
}
