using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace hOps.web.Migrations
{
    /// <inheritdoc />
    public partial class AddFloorId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RoomLayouts_Rooms_RoomId",
                table: "RoomLayouts");

            migrationBuilder.DropIndex(
                name: "IX_RoomLayouts_RoomId",
                table: "RoomLayouts");

            migrationBuilder.AddColumn<int>(
                name: "FloorId",
                table: "RoomLayouts",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FloorId",
                table: "RoomLayouts");

            migrationBuilder.CreateIndex(
                name: "IX_RoomLayouts_RoomId",
                table: "RoomLayouts",
                column: "RoomId");

            migrationBuilder.AddForeignKey(
                name: "FK_RoomLayouts_Rooms_RoomId",
                table: "RoomLayouts",
                column: "RoomId",
                principalTable: "Rooms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
