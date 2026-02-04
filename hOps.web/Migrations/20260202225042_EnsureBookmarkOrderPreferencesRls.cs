using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace hOps.web.Migrations
{
    /// <inheritdoc />
    public partial class EnsureBookmarkOrderPreferencesRls : Migration
    {
        private const string TableName = "BookmarkOrderPreferences";
        private const string SchemaName = "public";
        private const string PolicyName = TableName + "_service_role_policy";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider != "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                return;
            }

            var sql = $"""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.tables
                        WHERE table_schema = '{SchemaName}'
                          AND table_name = '{TableName}'
                    ) THEN
                        EXECUTE 'ALTER TABLE "{SchemaName}"."{TableName}" ENABLE ROW LEVEL SECURITY';
                        EXECUTE 'ALTER TABLE "{SchemaName}"."{TableName}" FORCE ROW LEVEL SECURITY';
                        EXECUTE 'DROP POLICY IF EXISTS "{PolicyName}" ON "{SchemaName}"."{TableName}"';
                        EXECUTE 'CREATE POLICY "{PolicyName}"
                            ON "{SchemaName}"."{TableName}"
                            FOR ALL
                            USING (
                                COALESCE(current_setting(''request.jwt.claim.role'', true), '''') IN ('''', ''service_role'')
                            )
                            WITH CHECK (
                                COALESCE(current_setting(''request.jwt.claim.role'', true), '''') IN ('''', ''service_role'')
                            )';
                    END IF;
                END
                $$;
            """;

            migrationBuilder.Sql(sql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider != "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                return;
            }

            var dropSql = $"""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.tables
                        WHERE table_schema = '{SchemaName}'
                          AND table_name = '{TableName}'
                    ) THEN
                        EXECUTE 'DROP POLICY IF EXISTS "{PolicyName}" ON "{SchemaName}"."{TableName}"';
                        EXECUTE 'ALTER TABLE "{SchemaName}"."{TableName}" NO FORCE ROW LEVEL SECURITY';
                        EXECUTE 'ALTER TABLE "{SchemaName}"."{TableName}" DISABLE ROW LEVEL SECURITY';
                    END IF;
                END
                $$;
            """;

            migrationBuilder.Sql(dropSql);
        }
    }
}
