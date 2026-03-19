using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace hOps.web.Migrations
{
    /// <inheritdoc />
    public partial class AddMprDeepCleanRoomDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeepCleanRoomNumbers",
                table: "HousekeepingMprEntries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HousekeepingMprEntryId",
                table: "DeepCleanSessions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeepCleanSessions_HousekeepingMprEntryId",
                table: "DeepCleanSessions",
                column: "HousekeepingMprEntryId");

            migrationBuilder.AddForeignKey(
                name: "FK_DeepCleanSessions_HousekeepingMprEntries_HousekeepingMprEnt~",
                table: "DeepCleanSessions",
                column: "HousekeepingMprEntryId",
                principalTable: "HousekeepingMprEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DeepCleanSessions_HousekeepingMprEntries_HousekeepingMprEnt~",
                table: "DeepCleanSessions");

            migrationBuilder.DropIndex(
                name: "IX_DeepCleanSessions_HousekeepingMprEntryId",
                table: "DeepCleanSessions");

            migrationBuilder.DropColumn(
                name: "DeepCleanRoomNumbers",
                table: "HousekeepingMprEntries");

            migrationBuilder.DropColumn(
                name: "HousekeepingMprEntryId",
                table: "DeepCleanSessions");
        }
    }
}
