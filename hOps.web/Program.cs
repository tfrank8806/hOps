using hOps.web.Data;
using hOps.web.Models;
using hOps.web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using Microsoft.Data.Sqlite;

var builder = WebApplication.CreateBuilder(args);

// ----------------------
// 1. Services Registration
// ----------------------

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// Register email sender
builder.Services.AddTransient<IEmailSender, EmailSender>();

// Add session support
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// ----------------------
// 2. Migrate DB & Seed Roles/Admin User
// ----------------------

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    var dbContext = services.GetRequiredService<ApplicationDbContext>();
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

    // Apply pending migrations with fallback support for legacy schemas
    await ApplyMigrationsWithLegacySupportAsync(dbContext);

    await EnsureProfilePhotoPathColumnAsync(dbContext);
    await EnsureRoomLayoutShapeColumnsAsync(dbContext);

    await SeedRolesAsync(roleManager);
    await SeedAdminUserAsync(userManager, roleManager);
}

// ----------------------
// 3. Configure Middleware
// ----------------------

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Enable session before authentication
app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

app.Run();

// ----------------------
// 4. Seed Helpers
// ----------------------

static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
{
    var roles = new[] { "Admin", "Manager", "User" };

    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }
}

static async Task SeedAdminUserAsync(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
{
    const string adminEmail = "admin@hotelops.local";
    const string adminPassword = "Admin@1234";

    var adminUser = await userManager.FindByEmailAsync(adminEmail);
    if (adminUser == null)
    {
        var user = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            FirstName = "Super",
            LastName = "Admin",
            MobilePhone = "1234567890"
        };

        var result = await userManager.CreateAsync(user, adminPassword);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(user, "Admin");
        }
        else
        {
            Console.WriteLine("Failed to create default admin user:");
            foreach (var error in result.Errors)
            {
                Console.WriteLine($" - {error.Description}");
            }
        }
    }
}

static async Task EnsureProfilePhotoPathColumnAsync(ApplicationDbContext dbContext)
{
    var connection = dbContext.Database.GetDbConnection();
    var shouldCloseConnection = connection.State != ConnectionState.Open;

    if (shouldCloseConnection)
    {
        await connection.OpenAsync();
    }

    try
    {
        await using var checkCommand = connection.CreateCommand();
        checkCommand.CommandText = "PRAGMA table_info('AspNetUsers');";

        var columnExists = false;

        await using (var reader = await checkCommand.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var columnName = reader.GetString(1);
                if (string.Equals(columnName, "ProfilePhotoPath", StringComparison.OrdinalIgnoreCase))
                {
                    columnExists = true;
                    break;
                }
            }
        }

        if (!columnExists)
        {
            await using var alterCommand = connection.CreateCommand();
            alterCommand.CommandText = "ALTER TABLE \"AspNetUsers\" ADD COLUMN \"ProfilePhotoPath\" TEXT;";
            await alterCommand.ExecuteNonQueryAsync();
        }
    }
    finally
    {
        if (shouldCloseConnection)
        {
            await connection.CloseAsync();
        }
    }
}

static async Task EnsureRoomLayoutShapeColumnsAsync(ApplicationDbContext dbContext)
{
    var connection = dbContext.Database.GetDbConnection();
    var shouldCloseConnection = connection.State != ConnectionState.Open;

    if (shouldCloseConnection)
    {
        await connection.OpenAsync();
    }

    try
    {
        var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await using (var checkCommand = connection.CreateCommand())
        {
            checkCommand.CommandText = "PRAGMA table_info('RoomLayouts');";

            await using var reader = await checkCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                existingColumns.Add(reader.GetString(1));
            }
        }

        var missingColumns = new List<(string Name, string Sql)>();

        if (!existingColumns.Contains("ShapeType"))
        {
            missingColumns.Add(("ShapeType", "ALTER TABLE \"RoomLayouts\" ADD COLUMN \"ShapeType\" TEXT;"));
        }

        if (!existingColumns.Contains("ShapeData"))
        {
            missingColumns.Add(("ShapeData", "ALTER TABLE \"RoomLayouts\" ADD COLUMN \"ShapeData\" TEXT;"));
        }

        foreach (var (_, sql) in missingColumns)
        {
            await using var alterCommand = connection.CreateCommand();
            alterCommand.CommandText = sql;
            await alterCommand.ExecuteNonQueryAsync();
        }
    }
    finally
    {
        if (shouldCloseConnection)
        {
            await connection.CloseAsync();
        }
    }
}

static async Task ApplyMigrationsWithLegacySupportAsync(ApplicationDbContext dbContext)
{
    await EnsureLegacyPassOnLogMigrationAsync(dbContext);

    try
    {
        await dbContext.Database.MigrateAsync();
    }
    catch (SqliteException ex) when (IsMissingWorkOrderTypeTableError(ex))
    {
        await EnsureLegacyWorkOrderTypeTableAsync(dbContext);
        await dbContext.Database.MigrateAsync();
    }
    catch (SqliteException ex) when (IsLegacyPassOnLogSchemaError(ex))
    {
        await EnsureLegacyPassOnLogMigrationAsync(dbContext);
        await dbContext.Database.MigrateAsync();
    }

    await ConsolidateLegacyWorkOrderTypeTableAsync(dbContext);
}

static bool IsMissingWorkOrderTypeTableError(SqliteException ex)
    => ex.SqliteErrorCode == 1 && ex.Message.Contains("no such table: WorkOrderType", StringComparison.OrdinalIgnoreCase);

static bool IsLegacyPassOnLogSchemaError(SqliteException ex)
    => ex.SqliteErrorCode == 11 && ex.Message.Contains("PassOnLog", StringComparison.OrdinalIgnoreCase);

static async Task EnsureLegacyWorkOrderTypeTableAsync(ApplicationDbContext dbContext)
{
    var connection = dbContext.Database.GetDbConnection();
    var shouldCloseConnection = connection.State != ConnectionState.Open;

    if (shouldCloseConnection)
    {
        await connection.OpenAsync();
    }

    try
    {
        await using var createCommand = connection.CreateCommand();
        createCommand.CommandText =
            """
            CREATE TABLE IF NOT EXISTS "WorkOrderType" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_WorkOrderType" PRIMARY KEY AUTOINCREMENT,
                "Name" TEXT NOT NULL,
                "Color" TEXT NOT NULL
            );
            """;

        await createCommand.ExecuteNonQueryAsync();
    }
    finally
    {
        if (shouldCloseConnection)
        {
            await connection.CloseAsync();
        }
    }
}

static async Task ConsolidateLegacyWorkOrderTypeTableAsync(ApplicationDbContext dbContext)
{
    var connection = dbContext.Database.GetDbConnection();
    var shouldCloseConnection = connection.State != ConnectionState.Open;

    if (shouldCloseConnection)
    {
        await connection.OpenAsync();
    }

    try
    {
        if (!await TableExistsAsync(connection, "WorkOrderType"))
        {
            return;
        }

        if (!await TableExistsAsync(connection, "WorkOrderTypes"))
        {
            return;
        }

        await using var syncCommand = connection.CreateCommand();
        syncCommand.CommandText =
            """
            INSERT INTO "WorkOrderTypes" ("Id", "Name", "Color")
            SELECT legacy."Id", legacy."Name", legacy."Color"
            FROM "WorkOrderType" AS legacy
            WHERE NOT EXISTS (
                SELECT 1 FROM "WorkOrderTypes" AS current WHERE current."Id" = legacy."Id"
            );
            """;

        await syncCommand.ExecuteNonQueryAsync();

        await using var dropCommand = connection.CreateCommand();
        dropCommand.CommandText = "DROP TABLE IF EXISTS \"WorkOrderType\";";
        await dropCommand.ExecuteNonQueryAsync();
    }
    finally
    {
        if (shouldCloseConnection)
        {
            await connection.CloseAsync();
        }
    }
}

static async Task<bool> TableExistsAsync(DbConnection connection, string tableName)
{
    try
    {
        await using var checkCommand = connection.CreateCommand();
        checkCommand.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name = @name LIMIT 1;";

        var parameter = checkCommand.CreateParameter();
        parameter.ParameterName = "@name";
        parameter.Value = tableName;
        checkCommand.Parameters.Add(parameter);

        var result = await checkCommand.ExecuteScalarAsync();
        return result != null;
    }
    catch (SqliteException ex) when (IsDuplicateTableSchemaError(ex, tableName))
    {
        // Legacy databases may contain duplicate schema entries which surface as
        // "malformed database schema" errors even though the table already exists.
        // Treat this as the table being present so the legacy migration logic can continue.
        return true;
    }
}

static bool IsDuplicateTableSchemaError(SqliteException ex, string tableName)
{
    if (ex.SqliteErrorCode != 11)
    {
        return false;
    }

    var message = ex.Message ?? string.Empty;

    if (!message.Contains("malformed database schema", StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    if (message.Contains(tableName, StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    return message.Contains("already exists", StringComparison.OrdinalIgnoreCase);
}

static async Task EnsureLegacyPassOnLogMigrationAsync(ApplicationDbContext dbContext)
{
    var connection = dbContext.Database.GetDbConnection();
    var shouldCloseConnection = connection.State != ConnectionState.Open;

    if (shouldCloseConnection)
    {
        await connection.OpenAsync();
    }

    try
    {
        var requiredTables = new[]
        {
            "PassOnLogs",
            "PassOnLogComments",
            "PassOnLogProperties",
            "PassOnLogViews"
        };

        foreach (var table in requiredTables)
        {
            if (!await TableExistsAsync(connection, table))
            {
                return;
            }
        }

        await EnsureMigrationsHistoryTableAsync(connection);

        const string migrationId = "20251018090000_AddPassOnLogs";
        if (await MigrationHistoryContainsAsync(connection, migrationId))
        {
            return;
        }

        await using var insertCommand = connection.CreateCommand();
        insertCommand.CommandText =
            "INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES (@id, @version);";

        var migrationIdParameter = insertCommand.CreateParameter();
        migrationIdParameter.ParameterName = "@id";
        migrationIdParameter.Value = migrationId;
        insertCommand.Parameters.Add(migrationIdParameter);

        var versionParameter = insertCommand.CreateParameter();
        versionParameter.ParameterName = "@version";
        versionParameter.Value = "9.0.9";
        insertCommand.Parameters.Add(versionParameter);

        await insertCommand.ExecuteNonQueryAsync();
    }
    finally
    {
        if (shouldCloseConnection)
        {
            await connection.CloseAsync();
        }
    }
}

static async Task EnsureMigrationsHistoryTableAsync(DbConnection connection)
{
    await using var createCommand = connection.CreateCommand();
    createCommand.CommandText =
        """
        CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
            "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
            "ProductVersion" TEXT NOT NULL
        );
        """;

    await createCommand.ExecuteNonQueryAsync();
}

static async Task<bool> MigrationHistoryContainsAsync(DbConnection connection, string migrationId)
{
    await using var checkCommand = connection.CreateCommand();
    checkCommand.CommandText =
        "SELECT 1 FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" = @id LIMIT 1;";

    var parameter = checkCommand.CreateParameter();
    parameter.ParameterName = "@id";
    parameter.Value = migrationId;
    checkCommand.Parameters.Add(parameter);

    var result = await checkCommand.ExecuteScalarAsync();
    return result != null;
}
