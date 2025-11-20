using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace hOps.web.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesLeadSubmissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SalesLeadSubmissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PropertyId = table.Column<int>(type: "int", nullable: false),
                    SalesContactId = table.Column<int>(type: "int", nullable: false),
                    SubmittedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    SubmittedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    GroupName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContactName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContactPhone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ContactEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NumberOfRooms = table.Column<int>(type: "int", nullable: true),
                    NumberOfGuests = table.Column<int>(type: "int", nullable: true),
                    BudgetMinimum = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    BudgetMaximum = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    EventStartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EventEndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InquiryTypes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    InquiryOtherDetails = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AdditionalDetails = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesLeadSubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesLeadSubmissions_AspNetUsers_SubmittedByUserId",
                        column: x => x.SubmittedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SalesLeadSubmissions_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SalesLeadSubmissions_SalesContacts_SalesContactId",
                        column: x => x.SalesContactId,
                        principalTable: "SalesContacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SalesLeadSubmissions_PropertyId_CreatedAtUtc",
                table: "SalesLeadSubmissions",
                columns: new[] { "PropertyId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesLeadSubmissions_SalesContactId",
                table: "SalesLeadSubmissions",
                column: "SalesContactId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLeadSubmissions_SubmittedByUserId",
                table: "SalesLeadSubmissions",
                column: "SubmittedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SalesLeadSubmissions");
        }
    }
}
