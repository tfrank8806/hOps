using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace hOps.web.Migrations
{
    /// <inheritdoc />
    public partial class SupplyInventoryPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SupplyInventorySnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PropertyId = table.Column<int>(type: "integer", nullable: false),
                    MonthlyBudget = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalInventoryValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalOrderCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DataJson = table.Column<string>(type: "text", nullable: false),
                    SavedByUserId = table.Column<string>(type: "text", nullable: false),
                    SavedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplyInventorySnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplyInventorySnapshots_AspNetUsers_SavedByUserId",
                        column: x => x.SavedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplyInventorySnapshots_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupplyInventoryStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PropertyId = table.Column<int>(type: "integer", nullable: false),
                    MonthlyBudget = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalInventoryValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalOrderCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DataJson = table.Column<string>(type: "text", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplyInventoryStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplyInventoryStates_AspNetUsers_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplyInventoryStates_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupplyInventorySnapshots_PropertyId_SavedAtUtc",
                table: "SupplyInventorySnapshots",
                columns: new[] { "PropertyId", "SavedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplyInventorySnapshots_SavedByUserId",
                table: "SupplyInventorySnapshots",
                column: "SavedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplyInventoryStates_PropertyId",
                table: "SupplyInventoryStates",
                column: "PropertyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplyInventoryStates_UpdatedByUserId",
                table: "SupplyInventoryStates",
                column: "UpdatedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SupplyInventorySnapshots");

            migrationBuilder.DropTable(
                name: "SupplyInventoryStates");
        }
    }
}
