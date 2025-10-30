using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace hOps.web.Migrations
{
    /// <inheritdoc />
    public partial class RebuildUserPropertyEmailSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserPropertyEmailSubscriptions");

            migrationBuilder.CreateTable(
                name: "UserPropertyEmailSubscriptions",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    PropertyId = table.Column<int>(type: "INTEGER", nullable: false),
                    IncludeInLogAlerts = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    IncludeInDailySummary = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    IncludeInWorkOrderAlerts = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPropertyEmailSubscriptions", x => new { x.UserId, x.PropertyId });
                    table.ForeignKey(
                        name: "FK_UserPropertyEmailSubscriptions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserPropertyEmailSubscriptions_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserPropertyEmailSubscriptions_PropertyId",
                table: "UserPropertyEmailSubscriptions",
                column: "PropertyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserPropertyEmailSubscriptions");

            migrationBuilder.CreateTable(
                name: "UserPropertyEmailSubscriptions",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    PropertyId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPropertyEmailSubscriptions", x => new { x.UserId, x.PropertyId });
                    table.ForeignKey(
                        name: "FK_UserPropertyEmailSubscriptions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserPropertyEmailSubscriptions_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserPropertyEmailSubscriptions_PropertyId",
                table: "UserPropertyEmailSubscriptions",
                column: "PropertyId");
        }
    }
}
