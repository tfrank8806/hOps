using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace hOps.web.Migrations
{
    /// <inheritdoc />
    public partial class AddPassOnLogAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "TimeZoneId",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: false,
                defaultValue: "Central Standard Time",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldDefaultValue: "UTC");

            migrationBuilder.CreateTable(
                name: "PassOnLogAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PassOnLogId = table.Column<int>(type: "INTEGER", nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    OriginalFileName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PassOnLogAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PassOnLogAttachments_PassOnLogs_PassOnLogId",
                        column: x => x.PassOnLogId,
                        principalTable: "PassOnLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PassOnLogAttachments_PassOnLogId",
                table: "PassOnLogAttachments",
                column: "PassOnLogId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PassOnLogAttachments");

            migrationBuilder.AlterColumn<string>(
                name: "TimeZoneId",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: false,
                defaultValue: "UTC",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldDefaultValue: "Central Standard Time");
        }
    }
}
