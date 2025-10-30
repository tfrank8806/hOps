using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace hOps.web.Migrations
{
    /// <inheritdoc />
    public partial class PropertyScopedSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PropertyId",
                table: "WorkOrderTypes",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PropertyId",
                table: "PhonebookTypes",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PropertyId",
                table: "Departments",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PropertyId",
                table: "CalendarCategories",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderTypes_PropertyId",
                table: "WorkOrderTypes",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_PhonebookTypes_PropertyId",
                table: "PhonebookTypes",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_PropertyId",
                table: "Departments",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarCategories_PropertyId",
                table: "CalendarCategories",
                column: "PropertyId");

            migrationBuilder.AddForeignKey(
                name: "FK_CalendarCategories_Properties_PropertyId",
                table: "CalendarCategories",
                column: "PropertyId",
                principalTable: "Properties",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Departments_Properties_PropertyId",
                table: "Departments",
                column: "PropertyId",
                principalTable: "Properties",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PhonebookTypes_Properties_PropertyId",
                table: "PhonebookTypes",
                column: "PropertyId",
                principalTable: "Properties",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkOrderTypes_Properties_PropertyId",
                table: "WorkOrderTypes",
                column: "PropertyId",
                principalTable: "Properties",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CalendarCategories_Properties_PropertyId",
                table: "CalendarCategories");

            migrationBuilder.DropForeignKey(
                name: "FK_Departments_Properties_PropertyId",
                table: "Departments");

            migrationBuilder.DropForeignKey(
                name: "FK_PhonebookTypes_Properties_PropertyId",
                table: "PhonebookTypes");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkOrderTypes_Properties_PropertyId",
                table: "WorkOrderTypes");

            migrationBuilder.DropIndex(
                name: "IX_WorkOrderTypes_PropertyId",
                table: "WorkOrderTypes");

            migrationBuilder.DropIndex(
                name: "IX_PhonebookTypes_PropertyId",
                table: "PhonebookTypes");

            migrationBuilder.DropIndex(
                name: "IX_Departments_PropertyId",
                table: "Departments");

            migrationBuilder.DropIndex(
                name: "IX_CalendarCategories_PropertyId",
                table: "CalendarCategories");

            migrationBuilder.DropColumn(
                name: "PropertyId",
                table: "WorkOrderTypes");

            migrationBuilder.DropColumn(
                name: "PropertyId",
                table: "PhonebookTypes");

            migrationBuilder.DropColumn(
                name: "PropertyId",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "PropertyId",
                table: "CalendarCategories");
        }
    }
}
