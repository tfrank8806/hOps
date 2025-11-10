using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace hOps.web.Migrations
{
    /// <inheritdoc />
    public partial class UserToDoItemsAndDeptAssignmentsUI : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserToDoItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    IsCompleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    WorkOrderId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserToDoItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserToDoItems_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserToDoItems_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserToDoItems_UserId",
                table: "UserToDoItems",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserToDoItems_WorkOrderId",
                table: "UserToDoItems",
                column: "WorkOrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserToDoItems");
        }
    }
}
