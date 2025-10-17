using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace hOps.web.Migrations
{
    /// <inheritdoc />
    public partial class AddPassOnLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PassOnLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedById = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PassOnLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PassOnLogs_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PassOnLogComments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PassOnLogId = table.Column<int>(type: "INTEGER", nullable: false),
                    Body = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedById = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PassOnLogComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PassOnLogComments_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PassOnLogComments_PassOnLogs_PassOnLogId",
                        column: x => x.PassOnLogId,
                        principalTable: "PassOnLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PassOnLogProperties",
                columns: table => new
                {
                    PassOnLogId = table.Column<int>(type: "INTEGER", nullable: false),
                    PropertyId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PassOnLogProperties", x => new { x.PassOnLogId, x.PropertyId });
                    table.ForeignKey(
                        name: "FK_PassOnLogProperties_PassOnLogs_PassOnLogId",
                        column: x => x.PassOnLogId,
                        principalTable: "PassOnLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PassOnLogProperties_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PassOnLogViews",
                columns: table => new
                {
                    PassOnLogId = table.Column<int>(type: "INTEGER", nullable: false),
                    ViewerId = table.Column<string>(type: "TEXT", nullable: false),
                    ViewedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PassOnLogViews", x => new { x.PassOnLogId, x.ViewerId });
                    table.ForeignKey(
                        name: "FK_PassOnLogViews_AspNetUsers_ViewerId",
                        column: x => x.ViewerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PassOnLogViews_PassOnLogs_PassOnLogId",
                        column: x => x.PassOnLogId,
                        principalTable: "PassOnLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PassOnLogComments_CreatedById",
                table: "PassOnLogComments",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_PassOnLogComments_PassOnLogId",
                table: "PassOnLogComments",
                column: "PassOnLogId");

            migrationBuilder.CreateIndex(
                name: "IX_PassOnLogProperties_PropertyId",
                table: "PassOnLogProperties",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_PassOnLogs_CreatedById",
                table: "PassOnLogs",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_PassOnLogViews_ViewerId",
                table: "PassOnLogViews",
                column: "ViewerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PassOnLogComments");

            migrationBuilder.DropTable(
                name: "PassOnLogProperties");

            migrationBuilder.DropTable(
                name: "PassOnLogViews");

            migrationBuilder.DropTable(
                name: "PassOnLogs");
        }
    }
}
