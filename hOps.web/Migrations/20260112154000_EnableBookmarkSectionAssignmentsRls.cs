using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace hOps.web.Migrations
{
    /// <inheritdoc />
    public partial class EnableBookmarkSectionAssignmentsRls : Migration
    {
        private const string TableName = "BookmarkSectionAssignments";
        private const string PolicyName = TableName + "_service_role_policy";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider != "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                return;
            }

            var policySql = $"""
                ALTER TABLE "{TableName}"
                    ENABLE ROW LEVEL SECURITY;

                ALTER TABLE "{TableName}"
                    FORCE ROW LEVEL SECURITY;

                DROP POLICY IF EXISTS "{PolicyName}" ON "{TableName}";
                CREATE POLICY "{PolicyName}"
                    ON "{TableName}"
                    FOR ALL
                    USING (
                        COALESCE(current_setting('request.jwt.claim.role', true), '') IN ('', 'service_role')
                    )
                    WITH CHECK (
                        COALESCE(current_setting('request.jwt.claim.role', true), '') IN ('', 'service_role')
                    );
            """;

            migrationBuilder.Sql(policySql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider != "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                return;
            }

            var dropSql = $"""
                DROP POLICY IF EXISTS "{PolicyName}" ON "{TableName}";

                ALTER TABLE "{TableName}"
                    NO FORCE ROW LEVEL SECURITY;

                ALTER TABLE "{TableName}"
                    DISABLE ROW LEVEL SECURITY;
            """;

            migrationBuilder.Sql(dropSql);
        }
    }
}
