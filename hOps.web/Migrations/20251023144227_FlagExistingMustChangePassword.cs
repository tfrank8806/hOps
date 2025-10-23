using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace hOps.web.Migrations
{
    /// <inheritdoc />
    public partial class FlagExistingMustChangePassword : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider == "Microsoft.EntityFrameworkCore.Sqlite")
            {
                migrationBuilder.Sql(
                    """
                    INSERT OR IGNORE INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                    SELECT '20251023144228_AddMustChangePasswordFlag', '8.0.20'
                    WHERE EXISTS (
                        SELECT 1
                        FROM pragma_table_info('AspNetUsers')
                        WHERE name = 'MustChangePassword'
                    );
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider == "Microsoft.EntityFrameworkCore.Sqlite")
            {
                migrationBuilder.Sql(
                    """
                    DELETE FROM "__EFMigrationsHistory"
                    WHERE "MigrationId" = '20251023144228_AddMustChangePasswordFlag';
                    """);
            }
        }
    }
}
