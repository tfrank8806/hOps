using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace hOps.web.Migrations
{
    /// <inheritdoc />
    public partial class DisableEmailAlertsByDefault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "AspNetUsers"
                SET
                    "EmailOnMessage" = FALSE,
                    "EmailOnMention" = FALSE,
                    "EmailOnWorkOrderDepartment" = FALSE,
                    "EmailOnLogEntry" = FALSE,
                    "EmailOnSchedulePosted" = FALSE;
            """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "AspNetUsers"
                SET
                    "EmailOnMessage" = TRUE,
                    "EmailOnMention" = TRUE,
                    "EmailOnWorkOrderDepartment" = TRUE,
                    "EmailOnLogEntry" = TRUE,
                    "EmailOnSchedulePosted" = TRUE;
            """);
        }
    }
}
