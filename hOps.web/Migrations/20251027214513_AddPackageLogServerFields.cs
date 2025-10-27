using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace hOps.web.Migrations
{
    /// <inheritdoc />
    public partial class AddPackageLogServerFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ArrivalDate",
                table: "PackageLogEntries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Delivered",
                table: "PackageLogEntries",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeliveredAt",
                table: "PackageLogEntries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DepartureDate",
                table: "PackageLogEntries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StorageLocation",
                table: "PackageLogEntries",
                type: "TEXT",
                maxLength: 150,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArrivalDate",
                table: "PackageLogEntries");

            migrationBuilder.DropColumn(
                name: "Delivered",
                table: "PackageLogEntries");

            migrationBuilder.DropColumn(
                name: "DeliveredAt",
                table: "PackageLogEntries");

            migrationBuilder.DropColumn(
                name: "DepartureDate",
                table: "PackageLogEntries");

            migrationBuilder.DropColumn(
                name: "StorageLocation",
                table: "PackageLogEntries");
        }
    }
}
