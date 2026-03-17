using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace hOps.web.Migrations
{
    /// <inheritdoc />
    public partial class AddHousekeepingMprTracker : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HousekeeperProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PropertyId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HousekeeperProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HousekeeperProfiles_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HousekeepingMprEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PropertyId = table.Column<int>(type: "integer", nullable: false),
                    HousekeeperId = table.Column<int>(type: "integer", nullable: true),
                    HousekeeperName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EntryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CheckoutRooms = table.Column<int>(type: "integer", nullable: false),
                    LinenChangeRooms = table.Column<int>(type: "integer", nullable: false),
                    StayoverRooms = table.Column<int>(type: "integer", nullable: false),
                    DndRooms = table.Column<int>(type: "integer", nullable: false),
                    HoursWorked = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    TotalMinutesWorked = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    MinutesPerRoom = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    DepartureStandardMinutes = table.Column<decimal>(type: "numeric", nullable: false),
                    LinenChangeStandardMinutes = table.Column<decimal>(type: "numeric", nullable: false),
                    StayoverStandardMinutes = table.Column<decimal>(type: "numeric", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HousekeepingMprEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HousekeepingMprEntries_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_HousekeepingMprEntries_HousekeeperProfiles_HousekeeperId",
                        column: x => x.HousekeeperId,
                        principalTable: "HousekeeperProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_HousekeepingMprEntries_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HousekeeperProfiles_PropertyId_Name",
                table: "HousekeeperProfiles",
                columns: new[] { "PropertyId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HousekeepingMprEntries_CreatedByUserId",
                table: "HousekeepingMprEntries",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_HousekeepingMprEntries_HousekeeperId",
                table: "HousekeepingMprEntries",
                column: "HousekeeperId");

            migrationBuilder.CreateIndex(
                name: "IX_HousekeepingMprEntries_PropertyId_EntryDate_HousekeeperId",
                table: "HousekeepingMprEntries",
                columns: new[] { "PropertyId", "EntryDate", "HousekeeperId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HousekeepingMprEntries");

            migrationBuilder.DropTable(
                name: "HousekeeperProfiles");
        }
    }
}
