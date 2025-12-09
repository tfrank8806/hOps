using hOps.web.Data;
using hOps.web.Models;
using hOps.web.Hubs;
using hOps.web.Services;
using hOps.web.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using System;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using Microsoft.Data.Sqlite;
using System.Linq;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

// ----------------------
// 1. Services Registration
// ----------------------

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? string.Empty;

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        var sqlitePath = Path.Combine(AppContext.BaseDirectory, "hOps.db");
        connectionString = $"Data Source={sqlitePath}";
    }

    // Prefer SQL Server for cloud/remote connection strings; fall back to SQLite for local file-based strings.
    var lc = connectionString.ToLowerInvariant();
    var prefersSqlServer = IsSqlServerConnectionString(connectionString, lc);

    if (prefersSqlServer)
    {
        options.UseSqlServer(connectionString);
        return;
    }

    options.UseSqlite(
        connectionString,
        sqliteOptions => sqliteOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
});

static bool IsSqlServerConnectionString(string connectionString, string lowerCasedConnectionString)
{
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return false;
    }

    // Quick exit for obvious SQLite patterns.
    if (lowerCasedConnectionString.Contains(".db", StringComparison.Ordinal) ||
        lowerCasedConnectionString.Contains(".sqlite", StringComparison.Ordinal) ||
        lowerCasedConnectionString.Contains("mode=memory", StringComparison.Ordinal))
    {
        return false;
    }

    // Common SQL Server markers.
    if (lowerCasedConnectionString.Contains("server=", StringComparison.Ordinal) ||
        lowerCasedConnectionString.Contains("data source=tcp:", StringComparison.Ordinal) ||
        lowerCasedConnectionString.Contains("database.windows.net", StringComparison.Ordinal) ||
        lowerCasedConnectionString.Contains("initial catalog=", StringComparison.Ordinal) ||
        lowerCasedConnectionString.Contains("authentication=active directory", StringComparison.Ordinal))
    {
        return true;
    }

    try
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        var dataSource = builder.DataSource?.Trim() ?? string.Empty;

        // Treat file-like data sources as SQLite.
        if (dataSource.EndsWith(".db", StringComparison.OrdinalIgnoreCase) ||
            dataSource.EndsWith(".db3", StringComparison.OrdinalIgnoreCase) ||
            dataSource.EndsWith(".sqlite", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // If the data source looks like a host:port or host,prefer SQL Server; otherwise, default to SQLite.
        if (dataSource.Contains(":", StringComparison.Ordinal) || dataSource.Contains(",", StringComparison.Ordinal))
        {
            return true;
        }

        // No path separators is an indicator of a server/instance name.
        if (!dataSource.Contains("/", StringComparison.Ordinal) && !dataSource.Contains("\\", StringComparison.Ordinal))
        {
            return true;
        }
    }
    catch
    {
    }

    return false;
}

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddSignalR();
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Authentication:Jwt"));
builder.Services.Configure<CaptchaOptions>(builder.Configuration.GetSection("Captcha"));
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddHttpClient<ICaptchaValidator, GoogleRecaptchaValidator>();

var jwtSettings = builder.Configuration.GetSection("Authentication:Jwt").Get<JwtOptions>() ?? new JwtOptions();
var jwtSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SigningKey));

builder.Services.AddAuthentication()
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = jwtSigningKey,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

// Register email sender
builder.Services.AddTransient<EmailSender>();
builder.Services.AddTransient<IEmailSender>(sp => sp.GetRequiredService<EmailSender>());
builder.Services.AddTransient<IExtendedEmailSender>(sp => sp.GetRequiredService<EmailSender>());
builder.Services.AddScoped<DirectMessageService>();
builder.Services.AddScoped<MentionService>();
builder.Services.AddScoped<IRealtimeNotificationService, RealtimeNotificationService>();
builder.Services.AddScoped<IPropertyAccessService, PropertyAccessService>();
builder.Services.AddSingleton<SchedulePdfRenderer>();
builder.Services.AddScoped<SchedulePublicationService>();
builder.Services.AddHostedService<DailySummaryEmailService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUserTimeZoneService, UserTimeZoneService>();

// Add session support
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
var httpsPort = builder.Configuration.GetValue<int?>("HttpsPort")
    ?? builder.Configuration.GetValue<int?>("ASPNETCORE_HTTPS_PORT");
builder.Services.AddHttpsRedirection(options =>
{
    options.HttpsPort = httpsPort;
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

    var isSqlite = dbContext.Database.IsSqlite();

    // Apply migrations with provider-specific handling; skip SQLite-only helpers for SQL Server.
    if (isSqlite)
    {
        await ApplyMigrationsWithLegacySupportAsync(dbContext);
        await EnsureProfilePhotoPathColumnAsync(dbContext);
        await EnsureRoomLayoutShapeColumnsAsync(dbContext);
    }
    else
    {
        await dbContext.Database.MigrateAsync();
    }

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
app.Use(async (context, next) =>
{
    if (context.User?.Identity?.IsAuthenticated == true)
    {
        var path = context.Request.Path;
        bool isAllowedPath =
            path.StartsWithSegments("/Identity/Account/ForceChangePassword", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments("/Identity/Account/Logout", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments("/Identity/Account/Manage", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments("/css", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments("/js", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments("/lib", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments("/images", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments("/favicon", StringComparison.OrdinalIgnoreCase);

        if (!isAllowedPath)
        {
            var userManager = context.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.GetUserAsync(context.User);
            if (user?.MustChangePassword == true)
            {
                context.Response.Redirect("/Identity/Account/ForceChangePassword");
                return;
            }
        }
    }

    await next();
});
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapHub<NotificationHub>("/hubs/notifications");
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
            MobilePhone = "1234567890",
            MustChangePassword = false
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
    else if (adminUser.MustChangePassword)
    {
        adminUser.MustChangePassword = false;
        await userManager.UpdateAsync(adminUser);
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

        if (!existingColumns.Contains("TextRotation"))
        {
            missingColumns.Add(("TextRotation", "ALTER TABLE \"RoomLayouts\" ADD COLUMN \"TextRotation\" INTEGER DEFAULT 0;"));
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
    catch (SqliteException ex) when (IsDuplicateTableCreateError(ex, "AspNetRoles"))
    {
        await EnsureInitialSqlServerMigrationRecordedAsync(dbContext);
        await dbContext.Database.MigrateAsync();
    }
    catch (SqliteException ex) when (IsDuplicateMustChangePasswordColumnError(ex))
    {
        await EnsureMustChangePasswordColumnAsync(dbContext);
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

static bool IsDuplicateMustChangePasswordColumnError(SqliteException ex)
    => ex.SqliteErrorCode == 1
       && ex.Message.Contains("duplicate column name: MustChangePassword", StringComparison.OrdinalIgnoreCase);

static async Task EnsureMustChangePasswordColumnAsync(ApplicationDbContext dbContext)
{
    var connection = dbContext.Database.GetDbConnection();
    var shouldCloseConnection = connection.State != ConnectionState.Open;

    if (shouldCloseConnection)
    {
        await connection.OpenAsync();
    }

    try
    {
        var hasColumn = false;

        await using (var pragmaCommand = connection.CreateCommand())
        {
            pragmaCommand.CommandText = "PRAGMA table_info('AspNetUsers');";
            await using var reader = await pragmaCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (string.Equals(reader.GetString(1), "MustChangePassword", StringComparison.OrdinalIgnoreCase))
                {
                    hasColumn = true;
                    break;
                }
            }
        }

        if (!hasColumn)
        {
            await using var addColumnCommand = connection.CreateCommand();
            addColumnCommand.CommandText =
                "ALTER TABLE \"AspNetUsers\" ADD COLUMN \"MustChangePassword\" INTEGER NOT NULL DEFAULT 0;";
            await addColumnCommand.ExecuteNonQueryAsync();
        }

        await EnsureMigrationRecordedAsync(connection, "20251023144228_AddMustChangePasswordFlag", "8.0.20");
    }
    finally
    {
        if (shouldCloseConnection)
        {
            await connection.CloseAsync();
        }
    }
}

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

static bool IsDuplicateTableCreateError(SqliteException ex, string tableName)
{
    if (ex.SqliteErrorCode != 1)
    {
        return false;
    }

    var message = ex.Message ?? string.Empty;
    if (string.IsNullOrWhiteSpace(message))
    {
        return false;
    }

    return message.Contains($"table \"{tableName}\" already exists", StringComparison.OrdinalIgnoreCase)
        || message.Contains($"table '{tableName}' already exists", StringComparison.OrdinalIgnoreCase)
        || message.Contains($"table {tableName} already exists", StringComparison.OrdinalIgnoreCase);
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
        await RemoveLegacyDuplicatePassOnLogSchemaEntriesAsync(connection);

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

        await using var insertCommand = connection.CreateCommand();
        insertCommand.CommandText =
            """
            INSERT OR IGNORE INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
            VALUES (@id, @version);
            """;

        var migrationIdParameter = insertCommand.CreateParameter();
        migrationIdParameter.ParameterName = "@id";
        migrationIdParameter.Value = migrationId;
        insertCommand.Parameters.Add(migrationIdParameter);

        var versionParameter = insertCommand.CreateParameter();
        versionParameter.ParameterName = "@version";
        versionParameter.Value = "9.0.9";
        insertCommand.Parameters.Add(versionParameter);

        try
        {
            await insertCommand.ExecuteNonQueryAsync();
        }
        catch (SqliteException ex) when (IsLegacyPassOnLogSchemaError(ex))
        {
            // The legacy schema defines duplicate Pass On Log tables which trigger malformed
            // schema errors when preparing the INSERT. Treat this as a no-op because the
            // existing tables already represent the applied migration.
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

static async Task EnsureInitialSqlServerMigrationRecordedAsync(ApplicationDbContext dbContext)
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
            "AspNetRoles",
            "AspNetUsers",
            "AspNetUserRoles"
        };

        foreach (var table in requiredTables)
        {
            if (!await TableExistsAsync(connection, table))
            {
                return;
            }
        }

        await EnsureMigrationRecordedAsync(connection, "20251117203459_InitialSqlServer", "8.0.20");
    }
    finally
    {
        if (shouldCloseConnection)
        {
            await connection.CloseAsync();
        }
    }
}

static async Task EnsureMigrationRecordedAsync(DbConnection connection, string migrationId, string productVersion)
{
    await EnsureMigrationsHistoryTableAsync(connection);

    await using var insertCommand = connection.CreateCommand();
    insertCommand.CommandText =
        """
        INSERT OR IGNORE INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
        VALUES (@id, @version);
        """;

    var migrationIdParameter = insertCommand.CreateParameter();
    migrationIdParameter.ParameterName = "@id";
    migrationIdParameter.Value = migrationId;
    insertCommand.Parameters.Add(migrationIdParameter);

    var versionParameter = insertCommand.CreateParameter();
    versionParameter.ParameterName = "@version";
    versionParameter.Value = productVersion;
    insertCommand.Parameters.Add(versionParameter);

    await insertCommand.ExecuteNonQueryAsync();
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

    try
    {
        await createCommand.ExecuteNonQueryAsync();
    }
    catch (SqliteException ex) when (IsDuplicateTableSchemaError(ex, "__EFMigrationsHistory")
        || IsLegacyPassOnLogSchemaError(ex))
    {
        // Treat legacy malformed schema errors as a no-op because the migrations history
        // table already exists in these databases.
    }
}

static async Task RemoveLegacyDuplicatePassOnLogSchemaEntriesAsync(DbConnection connection)
{
    var tables = new[]
    {
        "PassOnLogs",
        "PassOnLogComments",
        "PassOnLogProperties",
        "PassOnLogViews"
    };

    var duplicateRowIds = new List<long>();

    var writableSchemaEnabled = false;

    try
    {
        await using (var enableWritableSchema = connection.CreateCommand())
        {
            enableWritableSchema.CommandText = "PRAGMA writable_schema = 1;";
            await enableWritableSchema.ExecuteNonQueryAsync();
            writableSchemaEnabled = true;
        }

        foreach (var table in tables)
        {
            try
            {
                await using var lookupCommand = connection.CreateCommand();
                lookupCommand.CommandText =
                    """
                    SELECT rowid
                    FROM sqlite_master
                    WHERE type = 'table' AND name = @name
                    ORDER BY rowid;
                    """;

                var parameter = lookupCommand.CreateParameter();
                parameter.ParameterName = "@name";
                parameter.Value = table;
                lookupCommand.Parameters.Add(parameter);

                await using var reader = await lookupCommand.ExecuteReaderAsync();
                var rowIdsForTable = new List<long>();

                while (await reader.ReadAsync())
                {
                    if (!reader.IsDBNull(0))
                    {
                        rowIdsForTable.Add(reader.GetInt64(0));
                    }
                }

                if (rowIdsForTable.Count > 1)
                {
                    duplicateRowIds.AddRange(rowIdsForTable.Skip(1));
                }
            }
            catch (SqliteException ex) when (IsLegacyPassOnLogSchemaError(ex))
            {
                // If the schema remains malformed even with writable_schema enabled,
                // continue so the legacy migration can still progress.
            }
        }

        if (duplicateRowIds.Count == 0)
        {
            return;
        }

        foreach (var rowId in duplicateRowIds)
        {
            await using var deleteCommand = connection.CreateCommand();
            deleteCommand.CommandText = "DELETE FROM sqlite_master WHERE rowid = @rowid;";

            var parameter = deleteCommand.CreateParameter();
            parameter.ParameterName = "@rowid";
            parameter.Value = rowId;
            deleteCommand.Parameters.Add(parameter);

            await deleteCommand.ExecuteNonQueryAsync();
        }
    }
    finally
    {
        if (writableSchemaEnabled)
        {
            await using var disableWritableSchema = connection.CreateCommand();
            disableWritableSchema.CommandText = "PRAGMA writable_schema = 0;";
            await disableWritableSchema.ExecuteNonQueryAsync();
        }
    }
}




