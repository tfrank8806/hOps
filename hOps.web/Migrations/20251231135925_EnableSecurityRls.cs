using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace hOps.web.Migrations
{
    /// <inheritdoc />
    public partial class EnableSecurityRls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var tables = new[]
            {
                "RoomTypes",
                "UserAccessRequests",
                "WidgetMarketplaceModules",
                "AspNetRoles",
                "AspNetRoleClaims",
                "Properties",
                "AspNetUsers",
                "CalendarCategories",
                "Departments",
                "PhonebookTypes",
                "Rooms",
                "ScheduleShiftTemplates",
                "WorkOrderTypes",
                "AspNetUserClaims",
                "AspNetUserRoles",
                "AspNetUserLogins",
                "AspNetUserTokens",
                "Bookmarks",
                "BulletinPosts",
                "DirectMessageConversations",
                "DocumentFolders",
                "ManagerAnnouncements",
                "PackageLogEntries",
                "PassOnLogs",
                "SalesContacts",
                "ScheduleEmployees",
                "Schedules",
                "ScheduleSettings",
                "UserHomeLayouts",
                "UserPropertyAccesses",
                "UserPropertyEmailSubscriptions",
                "CalendarEvents",
                "UserDepartmentSubscriptions",
                "PhonebookContacts",
                "RoomLayouts",
                "WorkOrders",
                "BulletinPostAttachments",
                "DirectMessages",
                "DocumentFolderProperties",
                "Documents",
                "ManagerAnnouncementAttachments",
                "PassOnLogAttachments",
                "PassOnLogComments",
                "PassOnLogProperties",
                "PassOnLogViews",
                "SalesLeadSubmissions",
                "ScheduleTimeOffRequests",
                "ScheduleAssignments",
                "CalendarEventProperties",
                "UserToDoItems",
                "WorkOrderAttachments",
                "WorkOrderProperties",
                "UserNotifications",
                "DocumentProperties"
            };

            foreach (var table in tables)
            {
                ApplyServiceRoleOnlyRls(migrationBuilder, table);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            var tables = new[]
            {
                "RoomTypes",
                "UserAccessRequests",
                "WidgetMarketplaceModules",
                "AspNetRoles",
                "AspNetRoleClaims",
                "Properties",
                "AspNetUsers",
                "CalendarCategories",
                "Departments",
                "PhonebookTypes",
                "Rooms",
                "ScheduleShiftTemplates",
                "WorkOrderTypes",
                "AspNetUserClaims",
                "AspNetUserRoles",
                "AspNetUserLogins",
                "AspNetUserTokens",
                "Bookmarks",
                "BulletinPosts",
                "DirectMessageConversations",
                "DocumentFolders",
                "ManagerAnnouncements",
                "PackageLogEntries",
                "PassOnLogs",
                "SalesContacts",
                "ScheduleEmployees",
                "Schedules",
                "ScheduleSettings",
                "UserHomeLayouts",
                "UserPropertyAccesses",
                "UserPropertyEmailSubscriptions",
                "CalendarEvents",
                "UserDepartmentSubscriptions",
                "PhonebookContacts",
                "RoomLayouts",
                "WorkOrders",
                "BulletinPostAttachments",
                "DirectMessages",
                "DocumentFolderProperties",
                "Documents",
                "ManagerAnnouncementAttachments",
                "PassOnLogAttachments",
                "PassOnLogComments",
                "PassOnLogProperties",
                "PassOnLogViews",
                "SalesLeadSubmissions",
                "ScheduleTimeOffRequests",
                "ScheduleAssignments",
                "CalendarEventProperties",
                "UserToDoItems",
                "WorkOrderAttachments",
                "WorkOrderProperties",
                "UserNotifications",
                "DocumentProperties"
            };

            foreach (var table in tables)
            {
                RemoveServiceRoleOnlyRls(migrationBuilder, table);
            }
        }

        private static void ApplyServiceRoleOnlyRls(MigrationBuilder migrationBuilder, string tableName)
        {
            var policyName = $"{tableName}_service_role_policy";
            var policySql = $"""
                ALTER TABLE "{tableName}"
                    ENABLE ROW LEVEL SECURITY;

                ALTER TABLE "{tableName}"
                    FORCE ROW LEVEL SECURITY;

                DROP POLICY IF EXISTS "{policyName}" ON "{tableName}";
                CREATE POLICY "{policyName}"
                    ON "{tableName}"
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

        private static void RemoveServiceRoleOnlyRls(MigrationBuilder migrationBuilder, string tableName)
        {
            var policyName = $"{tableName}_service_role_policy";
            var dropSql = $"""
                DROP POLICY IF EXISTS "{policyName}" ON "{tableName}";

                ALTER TABLE "{tableName}"
                    NO FORCE ROW LEVEL SECURITY;

                ALTER TABLE "{tableName}"
                    DISABLE ROW LEVEL SECURITY;
            """;

            migrationBuilder.Sql(dropSql);
        }
    }
}
