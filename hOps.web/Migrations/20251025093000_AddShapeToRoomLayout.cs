using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace hOps.web.Migrations
{
    /// <inheritdoc />
    public partial class AddShapeToRoomLayout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ShapeData",
                table: "RoomLayouts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShapeType",
                table: "RoomLayouts",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShapeData",
                table: "RoomLayouts");

            migrationBuilder.DropColumn(
                name: "ShapeType",
                table: "RoomLayouts");
        }
    }
}
