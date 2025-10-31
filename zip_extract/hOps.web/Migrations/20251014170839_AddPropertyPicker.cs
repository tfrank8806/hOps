using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace hOps.web.Migrations
{
    /// <inheritdoc />
    public partial class AddPropertyPicker : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PropertyId1",
                table: "UserPropertyAccesses",
                type: "INTEGER",
                nullable: true);

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserPropertyAccesses_Properties_PropertyId1",
                table: "UserPropertyAccesses");

            migrationBuilder.DropIndex(
                name: "IX_UserPropertyAccesses_PropertyId1",
                table: "UserPropertyAccesses");

            migrationBuilder.DropColumn(
                name: "PropertyId1",
                table: "UserPropertyAccesses");
        }
    }
}
