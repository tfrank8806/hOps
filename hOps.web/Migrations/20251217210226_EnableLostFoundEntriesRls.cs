using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace hOps.web.Migrations
{
    /// <inheritdoc />
    public partial class EnableLostFoundEntriesRls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider != "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                return;
            }

            migrationBuilder.Sql("""
                ALTER TABLE "LostFoundEntries"
                    ENABLE ROW LEVEL SECURITY;

                ALTER TABLE "LostFoundEntries"
                    FORCE ROW LEVEL SECURITY;

                DROP POLICY IF EXISTS "LostFoundEntries_select_policy" ON "LostFoundEntries";
                CREATE POLICY "LostFoundEntries_select_policy"
                    ON "LostFoundEntries"
                    FOR SELECT
                    USING (
                        COALESCE(current_setting('request.jwt.claim.role', true), '') = 'service_role'
                        OR EXISTS (
                            SELECT 1
                            FROM "UserPropertyAccesses" upa
                            WHERE upa."PropertyId" = "LostFoundEntries"."PropertyId"
                              AND upa."ApplicationUserId" = COALESCE(
                                    current_setting('request.jwt.claim.user_id', true),
                                    current_setting('request.jwt.claim.sub', true),
                                    ''
                              )
                        )
                    );

                DROP POLICY IF EXISTS "LostFoundEntries_insert_policy" ON "LostFoundEntries";
                CREATE POLICY "LostFoundEntries_insert_policy"
                    ON "LostFoundEntries"
                    FOR INSERT
                    WITH CHECK (
                        COALESCE(current_setting('request.jwt.claim.role', true), '') = 'service_role'
                        OR EXISTS (
                            SELECT 1
                            FROM "UserPropertyAccesses" upa
                            WHERE upa."PropertyId" = "LostFoundEntries"."PropertyId"
                              AND upa."ApplicationUserId" = COALESCE(
                                    current_setting('request.jwt.claim.user_id', true),
                                    current_setting('request.jwt.claim.sub', true),
                                    ''
                              )
                        )
                    );

                DROP POLICY IF EXISTS "LostFoundEntries_update_policy" ON "LostFoundEntries";
                CREATE POLICY "LostFoundEntries_update_policy"
                    ON "LostFoundEntries"
                    FOR UPDATE
                    USING (
                        COALESCE(current_setting('request.jwt.claim.role', true), '') = 'service_role'
                        OR EXISTS (
                            SELECT 1
                            FROM "UserPropertyAccesses" upa
                            WHERE upa."PropertyId" = "LostFoundEntries"."PropertyId"
                              AND upa."ApplicationUserId" = COALESCE(
                                    current_setting('request.jwt.claim.user_id', true),
                                    current_setting('request.jwt.claim.sub', true),
                                    ''
                              )
                        )
                    )
                    WITH CHECK (
                        COALESCE(current_setting('request.jwt.claim.role', true), '') = 'service_role'
                        OR EXISTS (
                            SELECT 1
                            FROM "UserPropertyAccesses" upa
                            WHERE upa."PropertyId" = "LostFoundEntries"."PropertyId"
                              AND upa."ApplicationUserId" = COALESCE(
                                    current_setting('request.jwt.claim.user_id', true),
                                    current_setting('request.jwt.claim.sub', true),
                                    ''
                              )
                        )
                    );

                DROP POLICY IF EXISTS "LostFoundEntries_delete_policy" ON "LostFoundEntries";
                CREATE POLICY "LostFoundEntries_delete_policy"
                    ON "LostFoundEntries"
                    FOR DELETE
                    USING (
                        COALESCE(current_setting('request.jwt.claim.role', true), '') = 'service_role'
                        OR EXISTS (
                            SELECT 1
                            FROM "UserPropertyAccesses" upa
                            WHERE upa."PropertyId" = "LostFoundEntries"."PropertyId"
                              AND upa."ApplicationUserId" = COALESCE(
                                    current_setting('request.jwt.claim.user_id', true),
                                    current_setting('request.jwt.claim.sub', true),
                                    ''
                              )
                        )
                    );
            """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider != "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                return;
            }

            migrationBuilder.Sql("""
                DROP POLICY IF EXISTS "LostFoundEntries_select_policy" ON "LostFoundEntries";
                DROP POLICY IF EXISTS "LostFoundEntries_insert_policy" ON "LostFoundEntries";
                DROP POLICY IF EXISTS "LostFoundEntries_update_policy" ON "LostFoundEntries";
                DROP POLICY IF EXISTS "LostFoundEntries_delete_policy" ON "LostFoundEntries";

                ALTER TABLE "LostFoundEntries"
                    NO FORCE ROW LEVEL SECURITY;

                ALTER TABLE "LostFoundEntries"
                    DISABLE ROW LEVEL SECURITY;
            """);
        }
    }
}
