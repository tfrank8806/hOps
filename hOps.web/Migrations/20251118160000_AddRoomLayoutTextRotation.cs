using Microsoft.EntityFrameworkCore.Migrations;

namespace hOps.web.Migrations
{
    public partial class AddRoomLayoutTextRotation : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TextRotation",
                table: "RoomLayouts",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TextRotation",
                table: "RoomLayouts");
        }
    }
}
