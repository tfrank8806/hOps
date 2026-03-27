using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace hOps.web.Migrations
{
    /// <inheritdoc />
    public partial class EnableEfMigrationsHistoryRls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider != "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                return;
            }

            migrationBuilder.Sql("""
                ALTER TABLE "__EFMigrationsHistory"
                    ENABLE ROW LEVEL SECURITY;

                ALTER TABLE "__EFMigrationsHistory"
                    FORCE ROW LEVEL SECURITY;

                DROP POLICY IF EXISTS "__EFMigrationsHistory_select_policy" ON "__EFMigrationsHistory";
                CREATE POLICY "__EFMigrationsHistory_select_policy"
                    ON "__EFMigrationsHistory"
                    FOR SELECT
                    USING (
                        COALESCE(current_setting('request.jwt.claim.role', true), '') IN ('', 'service_role')
                    );

                DROP POLICY IF EXISTS "__EFMigrationsHistory_insert_policy" ON "__EFMigrationsHistory";
                CREATE POLICY "__EFMigrationsHistory_insert_policy"
                    ON "__EFMigrationsHistory"
                    FOR INSERT
                    WITH CHECK (
                        COALESCE(current_setting('request.jwt.claim.role', true), '') IN ('', 'service_role')
                    );

                DROP POLICY IF EXISTS "__EFMigrationsHistory_update_policy" ON "__EFMigrationsHistory";
                CREATE POLICY "__EFMigrationsHistory_update_policy"
                    ON "__EFMigrationsHistory"
                    FOR UPDATE
                    USING (
                        COALESCE(current_setting('request.jwt.claim.role', true), '') IN ('', 'service_role')
                    )
                    WITH CHECK (
                        COALESCE(current_setting('request.jwt.claim.role', true), '') IN ('', 'service_role')
                    );

                DROP POLICY IF EXISTS "__EFMigrationsHistory_delete_policy" ON "__EFMigrationsHistory";
                CREATE POLICY "__EFMigrationsHistory_delete_policy"
                    ON "__EFMigrationsHistory"
                    FOR DELETE
                    USING (
                        COALESCE(current_setting('request.jwt.claim.role', true), '') IN ('', 'service_role')
                    );
            """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider != "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                return;
            }

            migrationBuilder.Sql("""
                DROP POLICY IF EXISTS "__EFMigrationsHistory_select_policy" ON "__EFMigrationsHistory";
                DROP POLICY IF EXISTS "__EFMigrationsHistory_insert_policy" ON "__EFMigrationsHistory";
                DROP POLICY IF EXISTS "__EFMigrationsHistory_update_policy" ON "__EFMigrationsHistory";
                DROP POLICY IF EXISTS "__EFMigrationsHistory_delete_policy" ON "__EFMigrationsHistory";

                ALTER TABLE "__EFMigrationsHistory"
                    NO FORCE ROW LEVEL SECURITY;

                ALTER TABLE "__EFMigrationsHistory"
                    DISABLE ROW LEVEL SECURITY;
            """);
        }
    }
}
