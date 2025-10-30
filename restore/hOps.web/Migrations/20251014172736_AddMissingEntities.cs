using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace hOps.web.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserPropertyAccesses_Properties_PropertyId1",
                table: "UserPropertyAccesses");

            migrationBuilder.DropTable(
                name: "RoomTypes");

            migrationBuilder.DropIndex(
                name: "IX_UserPropertyAccesses_PropertyId1",
                table: "UserPropertyAccesses");

            migrationBuilder.DropColumn(
                name: "PropertyId1",
                table: "UserPropertyAccesses");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PropertyId1",
                table: "UserPropertyAccesses",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RoomTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomTypes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserPropertyAccesses_PropertyId1",
                table: "UserPropertyAccesses",
                column: "PropertyId1");

            migrationBuilder.AddForeignKey(
                name: "FK_UserPropertyAccesses_Properties_PropertyId1",
                table: "UserPropertyAccesses",
                column: "PropertyId1",
                principalTable: "Properties",
                principalColumn: "Id");
        }
    }
}
