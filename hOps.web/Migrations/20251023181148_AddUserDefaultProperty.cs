using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace hOps.web.Migrations
{
    /// <inheritdoc />
    public partial class AddUserDefaultProperty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DefaultPropertyId",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_DefaultPropertyId",
                table: "AspNetUsers",
                column: "DefaultPropertyId");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Properties_DefaultPropertyId",
                table: "AspNetUsers",
                column: "DefaultPropertyId",
                principalTable: "Properties",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Properties_DefaultPropertyId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_DefaultPropertyId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "DefaultPropertyId",
                table: "AspNetUsers");
        }
    }
}
