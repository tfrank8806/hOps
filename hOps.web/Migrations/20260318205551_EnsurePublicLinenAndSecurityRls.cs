using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace hOps.web.Migrations
{
    /// <inheritdoc />
    public partial class EnsurePublicLinenAndSecurityRls : Migration
    {
        private static readonly string[] Tables =
        {
            "DataProtectionKeys",
            "CalendarEventExceptions",
            "EmergencyLightTestEntries",
            "LinenInventoryItems",
            "LinenInventoryRoomTypes",
            "LinenInventoryItemRequirements",
            "LinenInventorySessionItems",
            "LinenInventorySessions",
            "LinenInventorySettings",
            "HousekeeperProfiles",
            "HousekeepingMprEntries"
        };

        private const string SchemaName = "public";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider != "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                return;
            }

            foreach (var table in Tables)
            {
                ApplyServiceRolePolicy(migrationBuilder, table);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider != "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                return;
            }

            foreach (var table in Tables)
            {
                RemoveServiceRolePolicy(migrationBuilder, table);
            }
        }

        private static void ApplyServiceRolePolicy(MigrationBuilder migrationBuilder, string tableName)
        {
            var policyName = $"{tableName}_service_role_policy";
            var sql = $"""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.tables
                        WHERE table_schema = '{SchemaName}'
                          AND table_name = '{tableName}'
                    ) THEN
                        EXECUTE 'ALTER TABLE "{SchemaName}"."{tableName}" ENABLE ROW LEVEL SECURITY';
                        EXECUTE 'ALTER TABLE "{SchemaName}"."{tableName}" FORCE ROW LEVEL SECURITY';
                        EXECUTE 'DROP POLICY IF EXISTS "{policyName}" ON "{SchemaName}"."{tableName}"';
                        EXECUTE 'CREATE POLICY "{policyName}"
                            ON "{SchemaName}"."{tableName}"
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

        private static void RemoveServiceRolePolicy(MigrationBuilder migrationBuilder, string tableName)
        {
            var policyName = $"{tableName}_service_role_policy";
            var sql = $"""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.tables
                        WHERE table_schema = '{SchemaName}'
                          AND table_name = '{tableName}'
                    ) THEN
                        EXECUTE 'DROP POLICY IF EXISTS "{policyName}" ON "{SchemaName}"."{tableName}"';
                        EXECUTE 'ALTER TABLE "{SchemaName}"."{tableName}" NO FORCE ROW LEVEL SECURITY';
                        EXECUTE 'ALTER TABLE "{SchemaName}"."{tableName}" DISABLE ROW LEVEL SECURITY';
                    END IF;
                END
                $$;
            """;

            migrationBuilder.Sql(sql);
        }
    }
}
