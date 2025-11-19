using Microsoft.EntityFrameworkCore.Migrations;

namespace hOps.web.Migrations
{
    public partial class MakeRoomLayoutRoomIdNullable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RoomLayouts_Rooms_RoomId",
                table: "RoomLayouts");

            migrationBuilder.AlterColumn<int>(
                name: "RoomId",
                table: "RoomLayouts",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddForeignKey(
                name: "FK_RoomLayouts_Rooms_RoomId",
                table: "RoomLayouts",
                column: "RoomId",
                principalTable: "Rooms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RoomLayouts_Rooms_RoomId",
                table: "RoomLayouts");

            migrationBuilder.AlterColumn<int>(
                name: "RoomId",
                table: "RoomLayouts",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_RoomLayouts_Rooms_RoomId",
                table: "RoomLayouts",
                column: "RoomId",
                principalTable: "Rooms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
