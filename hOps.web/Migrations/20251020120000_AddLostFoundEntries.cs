using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace hOps.web.Migrations
{
    /// <inheritdoc />
    public partial class AddLostFoundEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LostFoundEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    PropertyId = table.Column<int>(type: "INTEGER", nullable: false),
                    DateFound = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DateReportedLost = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FoundBy = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    GuestName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    GuestPhone = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    GuestAddress = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Location = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ItemFound = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ItemLost = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    Stored = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    PhotoPath = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedByDisplayName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LostFoundEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LostFoundEntries_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LostFoundEntries_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LostFoundEntries_CreatedByUserId",
                table: "LostFoundEntries",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LostFoundEntries_PropertyId",
                table: "LostFoundEntries",
                column: "PropertyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LostFoundEntries");
        }
    }
}
