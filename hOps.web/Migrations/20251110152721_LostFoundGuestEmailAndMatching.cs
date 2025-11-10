using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace hOps.web.Migrations
{
    /// <inheritdoc />
    public partial class LostFoundGuestEmailAndMatching : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GuestEmail",
                table: "LostFoundEntries",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MatchedEntryId",
                table: "LostFoundEntries",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LostFoundEntries_MatchedEntryId",
                table: "LostFoundEntries",
                column: "MatchedEntryId");

            migrationBuilder.AddForeignKey(
                name: "FK_LostFoundEntries_LostFoundEntries_MatchedEntryId",
                table: "LostFoundEntries",
                column: "MatchedEntryId",
                principalTable: "LostFoundEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LostFoundEntries_LostFoundEntries_MatchedEntryId",
                table: "LostFoundEntries");

            migrationBuilder.DropIndex(
                name: "IX_LostFoundEntries_MatchedEntryId",
                table: "LostFoundEntries");

            migrationBuilder.DropColumn(
                name: "GuestEmail",
                table: "LostFoundEntries");

            migrationBuilder.DropColumn(
                name: "MatchedEntryId",
                table: "LostFoundEntries");
        }
    }
}
