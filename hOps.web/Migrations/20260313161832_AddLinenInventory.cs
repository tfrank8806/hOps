using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace hOps.web.Migrations
{
    /// <inheritdoc />
    public partial class AddLinenInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LinenInventoryItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PropertyId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OrderItemNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    OrderCaseCount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OrderCasePrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ParLevelTarget = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LinenInventoryItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LinenInventoryItems_AspNetUsers_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LinenInventoryItems_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LinenInventoryRoomTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PropertyId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TotalRooms = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LinenInventoryRoomTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LinenInventoryRoomTypes_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LinenInventorySessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PropertyId = table.Column<int>(type: "integer", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    InventoryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MonthlyBudget = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PerformedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TotalCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ProjectedNeedCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LinenInventorySessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LinenInventorySessions_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LinenInventorySessions_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LinenInventorySettings",
                columns: table => new
                {
                    PropertyId = table.Column<int>(type: "integer", nullable: false),
                    PropertyLabel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DefaultMonthlyBudget = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LinenInventorySettings", x => x.PropertyId);
                    table.ForeignKey(
                        name: "FK_LinenInventorySettings_AspNetUsers_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LinenInventorySettings_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LinenInventoryItemRequirements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InventoryItemId = table.Column<int>(type: "integer", nullable: false),
                    RoomTypeId = table.Column<int>(type: "integer", nullable: false),
                    UnitsPerRoom = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LinenInventoryItemRequirements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LinenInventoryItemRequirements_LinenInventoryItems_Inventor~",
                        column: x => x.InventoryItemId,
                        principalTable: "LinenInventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LinenInventoryItemRequirements_LinenInventoryRoomTypes_Room~",
                        column: x => x.RoomTypeId,
                        principalTable: "LinenInventoryRoomTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LinenInventorySessionItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SessionId = table.Column<int>(type: "integer", nullable: false),
                    InventoryItemId = table.Column<int>(type: "integer", nullable: false),
                    LaundryClean = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    LaundryDirty = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    InStorage = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OnCarts = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalOnHand = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    LastMonthActuals = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    InRoomsQuantity = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    BudgetedPar = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OrderRecommendation = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ActToParRatio = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    CasesToOrder = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    NeedCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CasesPurchased = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OrderCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LinenInventorySessionItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LinenInventorySessionItems_LinenInventoryItems_InventoryIte~",
                        column: x => x.InventoryItemId,
                        principalTable: "LinenInventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LinenInventorySessionItems_LinenInventorySessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "LinenInventorySessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LinenInventoryItemRequirements_InventoryItemId_RoomTypeId",
                table: "LinenInventoryItemRequirements",
                columns: new[] { "InventoryItemId", "RoomTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LinenInventoryItemRequirements_RoomTypeId",
                table: "LinenInventoryItemRequirements",
                column: "RoomTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_LinenInventoryItems_PropertyId",
                table: "LinenInventoryItems",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_LinenInventoryItems_UpdatedByUserId",
                table: "LinenInventoryItems",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LinenInventoryRoomTypes_PropertyId",
                table: "LinenInventoryRoomTypes",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_LinenInventorySessionItems_InventoryItemId",
                table: "LinenInventorySessionItems",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_LinenInventorySessionItems_SessionId",
                table: "LinenInventorySessionItems",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_LinenInventorySessions_CreatedByUserId",
                table: "LinenInventorySessions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LinenInventorySessions_PropertyId_Year_Month",
                table: "LinenInventorySessions",
                columns: new[] { "PropertyId", "Year", "Month" });

            migrationBuilder.CreateIndex(
                name: "IX_LinenInventorySettings_UpdatedByUserId",
                table: "LinenInventorySettings",
                column: "UpdatedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LinenInventoryItemRequirements");

            migrationBuilder.DropTable(
                name: "LinenInventorySessionItems");

            migrationBuilder.DropTable(
                name: "LinenInventorySettings");

            migrationBuilder.DropTable(
                name: "LinenInventoryRoomTypes");

            migrationBuilder.DropTable(
                name: "LinenInventoryItems");

            migrationBuilder.DropTable(
                name: "LinenInventorySessions");
        }
    }
}
