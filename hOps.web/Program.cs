using hOps.web.Data;
using hOps.web.Models;
using hOps.web.Hubs;
using hOps.web.Services;
using hOps.web.Options;
using hOps.web.Infrastructure;
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
using Npgsql;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);
var forceSqlite = ShouldForceSqlite(builder.Configuration);

// ----------------------
// 1. Services Registration
// ----------------------

var dataProtectionKeysPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "DataProtectionKeys");
Directory.CreateDirectory(dataProtectionKeysPath);

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath))
    .SetApplicationName("hOps.web");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    var configuredConnectionString = ConnectionStringHelper.GetDefaultConnectionString(builder.Configuration);

    if (forceSqlite)
    {
        var sqliteConnectionString = ResolveSqliteConnectionString(configuredConnectionString);
        options.UseSqlite(
            sqliteConnectionString,
            sqliteOptions => sqliteOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
        return;
    }

    var connectionString = configuredConnectionString;

    if (string.IsNullOrWhiteSpace(connectionString) || !connectionString.Contains('='))
    {
        connectionString = ResolveSqliteConnectionString(connectionString);
    }

    // Prefer SQL Server for cloud/remote connection strings; fall back to SQLite for local file-based strings.
    var lc = connectionString.ToLowerInvariant();
    var prefersPostgres = IsPostgresConnectionString(connectionString, lc);

    if (prefersPostgres)
    {
        connectionString = ConnectionStringHelper.NormalizePostgresConnectionString(connectionString);

        options.UseNpgsql(
            connectionString,
            npgsqlOptions => npgsqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
        return;
    }

    var sqliteFallbackConnection = ResolveSqliteConnectionString(connectionString);

    options.UseSqlite(
        sqliteFallbackConnection,
        sqliteOptions => sqliteOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
});

static bool IsPostgresConnectionString(string connectionString, string lowerCasedConnectionString)
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

    if (lowerCasedConnectionString.Contains("postgresql://", StringComparison.Ordinal) ||
        lowerCasedConnectionString.Contains("host=", StringComparison.Ordinal) ||
        lowerCasedConnectionString.Contains("username=", StringComparison.Ordinal) ||
        lowerCasedConnectionString.Contains("user id=", StringComparison.Ordinal) ||
        lowerCasedConnectionString.Contains("port=5432", StringComparison.Ordinal))
    {
        return true;
    }

    try
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (!string.IsNullOrWhiteSpace(builder.Host))
        {
            return true;
        }
    }
    catch
    {
        // Ignore invalid connection strings – we'll fall back to SQLite.
    }

    return false;
}

static bool ShouldForceSqlite(IConfiguration configuration)
{
    if (TryParseBooleanSetting(configuration["Database:ForceSqlite"], out var configuredValue))
    {
        return configuredValue;
    }

    if (TryParseBooleanSetting(Environment.GetEnvironmentVariable("HOPS_FORCE_SQLITE"), out var envOverride))
    {
        return envOverride;
    }

    if (TryParseBooleanSetting(Environment.GetEnvironmentVariable("FORCE_SQLITE"), out var genericOverride))
    {
        return genericOverride;
    }

    return IsCiBuild();
}

static bool TryParseBooleanSetting(string? value, out bool parsed)
{
    parsed = false;

    if (string.IsNullOrWhiteSpace(value))
    {
        return false;
    }

    if (bool.TryParse(value, out parsed))
    {
        return true;
    }

    if (string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase))
    {
        parsed = true;
        return true;
    }

    if (string.Equals(value, "0", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "no", StringComparison.OrdinalIgnoreCase))
    {
        parsed = false;
        return true;
    }

    return false;
}

static bool IsCiBuild()
{
    if (EnvFlagEnabled("TF_BUILD", treatPresenceAsTrue: true))
    {
        return true;
    }

    if (EnvFlagEnabled("CI"))
    {
        return true;
    }

    if (EnvFlagEnabled("GITHUB_ACTIONS"))
    {
        return true;
    }

    var azureIndicators = new[]
    {
        "BUILD_BUILDID",
        "BUILD_BUILDNUMBER",
        "BUILD_DEFINITIONNAME",
        "SYSTEM_TEAMPROJECTID",
        "SYSTEM_COLLECTIONURI",
        "AGENT_ID"
    };

    if (azureIndicators.Any(name => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name))))
    {
        return true;
    }

    return false;
}

static bool EnvFlagEnabled(string name, bool treatPresenceAsTrue = false)
{
    var rawValue = Environment.GetEnvironmentVariable(name);
    if (string.IsNullOrWhiteSpace(rawValue))
    {
        return false;
    }

    var value = rawValue.Trim();

    if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "0", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "no", StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    return treatPresenceAsTrue;
}

static string ResolveSqliteConnectionString(string? configuredValue)
{
    if (!string.IsNullOrWhiteSpace(configuredValue))
    {
        var trimmed = configuredValue.Trim();
        var lower = trimmed.ToLowerInvariant();

        if (IsPostgresConnectionString(trimmed, lower))
        {
            return BuildSqliteDataSourceFromPath("hOps.db");
        }

        if (lower.Contains("data source=", StringComparison.Ordinal) ||
            lower.Contains("mode=memory", StringComparison.Ordinal))
        {
            return trimmed;
        }

        if (!trimmed.Contains('=') && !trimmed.Contains(';'))
        {
            return BuildSqliteDataSourceFromPath(trimmed);
        }

        if (lower.Contains(".db", StringComparison.Ordinal) ||
            lower.Contains(".sqlite", StringComparison.Ordinal))
        {
            return trimmed;
        }
    }

    return BuildSqliteDataSourceFromPath("hOps.db");
}

static string BuildSqliteDataSourceFromPath(string? relativeOrAbsolutePath)
{
    var path = relativeOrAbsolutePath;

    if (string.IsNullOrWhiteSpace(path))
    {
        path = "hOps.db";
    }

    if (!Path.IsPathRooted(path))
    {
        path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, path));
    }

    return $"Data Source={path}";
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

await ApplyMigrationsWithLegacySupportAsync(dbContext);

 if (isSqlite)
 {
     await EnsureProfilePhotoPathColumnAsync(dbContext);
     await EnsureRoomLayoutShapeColumnsAsync(dbContext);
     await EnsureUserHomeLayoutsTableAsync(dbContext);
     await EnsureSalesLeadSubmissionsTableAsync(dbContext);
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
        if (!await TableExistsAsync(connection, "AspNetUsers"))
        {
            return;
        }

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
        if (!await TableExistsAsync(connection, "RoomLayouts"))
        {
            return;
        }

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
        await EnsureInitialPostgresMigrationRecordedAsync(dbContext);
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
    catch (SqliteException ex) when (IsDuplicateBookmarkQuickFlagColumnError(ex))
    {
        await EnsureBookmarkQuickFlagMigrationRecordedAsync(dbContext);
        await dbContext.Database.MigrateAsync();
    }

    await ConsolidateLegacyWorkOrderTypeTableAsync(dbContext);
}

static async Task EnsureBookmarksTableAsync(ApplicationDbContext dbContext)
{
    var connection = dbContext.Database.GetDbConnection();
    var shouldCloseConnection = connection.State != ConnectionState.Open;

    if (shouldCloseConnection)
    {
        await connection.OpenAsync();
    }

    try
    {
        if (await TableExistsAsync(connection, "Bookmarks"))
        {
            return;
        }

        const string createTableSql =
            """
            CREATE TABLE IF NOT EXISTS "Bookmarks" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_Bookmarks" PRIMARY KEY AUTOINCREMENT,
                "Name" TEXT NOT NULL,
                "Url" TEXT NOT NULL,
                "Description" TEXT NULL,
                "Section" INTEGER NOT NULL,
                "ShowInQuickMenu" INTEGER NOT NULL DEFAULT 0,
                "CreatedById" TEXT NOT NULL,
                "PropertyId" INTEGER NULL,
                CONSTRAINT "FK_Bookmarks_AspNetUsers_CreatedById" FOREIGN KEY ("CreatedById") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_Bookmarks_Properties_PropertyId" FOREIGN KEY ("PropertyId") REFERENCES "Properties" ("Id") ON DELETE CASCADE
            );
            """;

        await using (var createCommand = connection.CreateCommand())
        {
            createCommand.CommandText = createTableSql;
            await createCommand.ExecuteNonQueryAsync();
        }

        const string createCreatedByIndex =
            """
            CREATE INDEX IF NOT EXISTS "IX_Bookmarks_CreatedById"
            ON "Bookmarks" ("CreatedById");
            """;

        const string createPropertyIndex =
            """
            CREATE INDEX IF NOT EXISTS "IX_Bookmarks_PropertyId"
            ON "Bookmarks" ("PropertyId");
            """;

        await using (var createdByIndexCommand = connection.CreateCommand())
        {
            createdByIndexCommand.CommandText = createCreatedByIndex;
            await createdByIndexCommand.ExecuteNonQueryAsync();
        }

        await using (var propertyIndexCommand = connection.CreateCommand())
        {
            propertyIndexCommand.CommandText = createPropertyIndex;
            await propertyIndexCommand.ExecuteNonQueryAsync();
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

static async Task EnsureBookmarkQuickFlagMigrationRecordedAsync(ApplicationDbContext dbContext)
{
    var connection = dbContext.Database.GetDbConnection();
    var shouldCloseConnection = connection.State != ConnectionState.Open;

    if (shouldCloseConnection)
    {
        await connection.OpenAsync();
    }

    try
    {
        await EnsureMigrationRecordedAsync(connection, "20251120024344_AddBookmarkQuickFlag", "8.0.20");
    }
    finally
    {
        if (shouldCloseConnection)
        {
            await connection.CloseAsync();
        }
    }
}

static async Task EnsureSalesLeadSubmissionsTableAsync(ApplicationDbContext dbContext)
{
    var connection = dbContext.Database.GetDbConnection();
    var shouldCloseConnection = connection.State != ConnectionState.Open;

    if (shouldCloseConnection)
    {
        await connection.OpenAsync();
    }

    try
    {
        if (!await TableExistsAsync(connection, "SalesLeadSubmissions"))
        {
            const string createTableSql =
                """
                CREATE TABLE IF NOT EXISTS "SalesLeadSubmissions" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_SalesLeadSubmissions" PRIMARY KEY AUTOINCREMENT,
                    "PropertyId" INTEGER NOT NULL,
                    "SalesContactId" INTEGER NOT NULL,
                    "SubmittedByUserId" TEXT NULL,
                    "SubmittedByName" TEXT NOT NULL,
                    "GroupName" TEXT NOT NULL,
                    "ContactName" TEXT NOT NULL,
                    "ContactPhone" TEXT NULL,
                    "ContactEmail" TEXT NOT NULL,
                    "NumberOfRooms" INTEGER NULL,
                    "NumberOfGuests" INTEGER NULL,
                    "BudgetMinimum" NUMERIC NULL,
                    "BudgetMaximum" NUMERIC NULL,
                    "EventStartDate" TEXT NULL,
                    "EventEndDate" TEXT NULL,
                    "InquiryTypes" TEXT NOT NULL,
                    "InquiryOtherDetails" TEXT NULL,
                    "AdditionalDetails" TEXT NULL,
                    "CreatedAtUtc" TEXT NOT NULL DEFAULT (datetime('now')),
                    CONSTRAINT "FK_SalesLeadSubmissions_Properties_PropertyId" FOREIGN KEY ("PropertyId") REFERENCES "Properties" ("Id") ON DELETE CASCADE,
                    CONSTRAINT "FK_SalesLeadSubmissions_SalesContacts_SalesContactId" FOREIGN KEY ("SalesContactId") REFERENCES "SalesContacts" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_SalesLeadSubmissions_AspNetUsers_SubmittedByUserId" FOREIGN KEY ("SubmittedByUserId") REFERENCES "AspNetUsers" ("Id") ON DELETE SET NULL
                );
                """;

            await using (var createCommand = connection.CreateCommand())
            {
                createCommand.CommandText = createTableSql;
                await createCommand.ExecuteNonQueryAsync();
            }

            const string propertyIndexSql =
                """
                CREATE INDEX IF NOT EXISTS "IX_SalesLeadSubmissions_PropertyId_CreatedAtUtc"
                ON "SalesLeadSubmissions" ("PropertyId", "CreatedAtUtc");
                """;

            await using (var propertyIndexCommand = connection.CreateCommand())
            {
                propertyIndexCommand.CommandText = propertyIndexSql;
                await propertyIndexCommand.ExecuteNonQueryAsync();
            }

            const string salesContactIndexSql =
                """
                CREATE INDEX IF NOT EXISTS "IX_SalesLeadSubmissions_SalesContactId"
                ON "SalesLeadSubmissions" ("SalesContactId");
                """;

            await using (var salesContactIndexCommand = connection.CreateCommand())
            {
                salesContactIndexCommand.CommandText = salesContactIndexSql;
                await salesContactIndexCommand.ExecuteNonQueryAsync();
            }
        }

        await EnsureMigrationRecordedAsync(connection, "20251120200115_AddSalesLeadSubmissions", "8.0.20");
    }
    finally
    {
        if (shouldCloseConnection)
        {
            await connection.CloseAsync();
        }
    }
}

static async Task EnsureUserNotificationsTableAsync(ApplicationDbContext dbContext)
{
    var connection = dbContext.Database.GetDbConnection();
    var shouldCloseConnection = connection.State != ConnectionState.Open;

    if (shouldCloseConnection)
    {
        await connection.OpenAsync();
    }

    try
    {
        if (!await TableExistsAsync(connection, "UserNotifications"))
        {
            const string createTableSql =
                """
                CREATE TABLE IF NOT EXISTS "UserNotifications" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_UserNotifications" PRIMARY KEY AUTOINCREMENT,
                    "UserId" TEXT NOT NULL,
                    "Type" TEXT NOT NULL,
                    "Title" TEXT NOT NULL,
                    "Content" TEXT NULL,
                    "LinkUrl" TEXT NULL,
                    "DirectMessageId" INTEGER NULL,
                    "PassOnLogId" INTEGER NULL,
                    "IsRead" INTEGER NOT NULL DEFAULT 0,
                    "CreatedAt" TEXT NOT NULL DEFAULT (datetime('now')),
                    "ReadAt" TEXT NULL,
                    CONSTRAINT "FK_UserNotifications_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE,
                    CONSTRAINT "FK_UserNotifications_DirectMessages_DirectMessageId" FOREIGN KEY ("DirectMessageId") REFERENCES "DirectMessages" ("Id") ON DELETE CASCADE,
                    CONSTRAINT "FK_UserNotifications_PassOnLogs_PassOnLogId" FOREIGN KEY ("PassOnLogId") REFERENCES "PassOnLogs" ("Id") ON DELETE RESTRICT
                );
                """;

            await using (var createCommand = connection.CreateCommand())
            {
                createCommand.CommandText = createTableSql;
                await createCommand.ExecuteNonQueryAsync();
            }

            const string userIndexSql =
                """
                CREATE INDEX IF NOT EXISTS "IX_UserNotifications_UserId"
                ON "UserNotifications" ("UserId");
                """;

            const string directMessageIndexSql =
                """
                CREATE INDEX IF NOT EXISTS "IX_UserNotifications_DirectMessageId"
                ON "UserNotifications" ("DirectMessageId");
                """;

            const string passOnLogIndexSql =
                """
                CREATE INDEX IF NOT EXISTS "IX_UserNotifications_PassOnLogId"
                ON "UserNotifications" ("PassOnLogId");
                """;

            await using (var userIndexCommand = connection.CreateCommand())
            {
                userIndexCommand.CommandText = userIndexSql;
                await userIndexCommand.ExecuteNonQueryAsync();
            }

            await using (var dmIndexCommand = connection.CreateCommand())
            {
                dmIndexCommand.CommandText = directMessageIndexSql;
                await dmIndexCommand.ExecuteNonQueryAsync();
            }

            await using (var passOnLogIndexCommand = connection.CreateCommand())
            {
                passOnLogIndexCommand.CommandText = passOnLogIndexSql;
                await passOnLogIndexCommand.ExecuteNonQueryAsync();
            }
        }

        await EnsureMigrationRecordedAsync(connection, "20251201015127_AddPassOnLogNotificationLink", "8.0.20");
    }
    finally
    {
        if (shouldCloseConnection)
        {
            await connection.CloseAsync();
        }
    }
}

static async Task EnsureUserHomeLayoutsTableAsync(ApplicationDbContext dbContext)
{
    var connection = dbContext.Database.GetDbConnection();
    var shouldCloseConnection = connection.State != ConnectionState.Open;

    if (shouldCloseConnection)
    {
        await connection.OpenAsync();
    }

    try
    {
        if (!await TableExistsAsync(connection, "UserHomeLayouts"))
        {
            const string createTableSql =
                """
                CREATE TABLE IF NOT EXISTS "UserHomeLayouts" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_UserHomeLayouts" PRIMARY KEY AUTOINCREMENT,
                    "UserId" TEXT NOT NULL,
                    "PersonaKey" TEXT NOT NULL,
                    "IsDefault" INTEGER NOT NULL DEFAULT 0,
                    "LayoutJson" TEXT NOT NULL,
                    "UpdatedAtUtc" TEXT NOT NULL DEFAULT (datetime('now')),
                    CONSTRAINT "FK_UserHomeLayouts_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
                );
                """;

            await using (var createCommand = connection.CreateCommand())
            {
                createCommand.CommandText = createTableSql;
                await createCommand.ExecuteNonQueryAsync();
            }

            const string personaIndexSql =
                """
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_UserHomeLayouts_UserId_PersonaKey"
                ON "UserHomeLayouts" ("UserId", "PersonaKey");
                """;

            await using (var personaIndexCommand = connection.CreateCommand())
            {
                personaIndexCommand.CommandText = personaIndexSql;
                await personaIndexCommand.ExecuteNonQueryAsync();
            }
        }

        if (!await TableExistsAsync(connection, "WidgetMarketplaceModules"))
        {
            const string createMarketplaceSql =
                """
                CREATE TABLE IF NOT EXISTS "WidgetMarketplaceModules" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_WidgetMarketplaceModules" PRIMARY KEY AUTOINCREMENT,
                    "WidgetId" TEXT NOT NULL,
                    "IsEnabled" INTEGER NOT NULL DEFAULT 0,
                    "UpdatedAtUtc" TEXT NOT NULL DEFAULT (datetime('now'))
                );
                """;

            await using (var createMarketplaceCommand = connection.CreateCommand())
            {
                createMarketplaceCommand.CommandText = createMarketplaceSql;
                await createMarketplaceCommand.ExecuteNonQueryAsync();
            }

            const string widgetIndexSql =
                """
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_WidgetMarketplaceModules_WidgetId"
                ON "WidgetMarketplaceModules" ("WidgetId");
                """;

            await using (var widgetIndexCommand = connection.CreateCommand())
            {
                widgetIndexCommand.CommandText = widgetIndexSql;
                await widgetIndexCommand.ExecuteNonQueryAsync();
            }
        }

        await EnsureMigrationRecordedAsync(connection, "20251202194530_AddUserHomeLayout", "8.0.20");
        await EnsureMigrationRecordedAsync(connection, "20251206045825_AddLayoutMarketplace", "8.0.20");
    }
    finally
    {
        if (shouldCloseConnection)
        {
            await connection.CloseAsync();
        }
    }
}

static async Task EnsureDailySummaryLastSentColumnAsync(ApplicationDbContext dbContext)
{
    var connection = dbContext.Database.GetDbConnection();
    var shouldCloseConnection = connection.State != ConnectionState.Open;

    if (shouldCloseConnection)
    {
        await connection.OpenAsync();
    }

    try
    {
        if (!await TableExistsAsync(connection, "AspNetUsers"))
        {
            return;
        }

        var hasDailySummaryColumn = false;
        var hasDefaultPropertyColumn = false;

        await using (var pragmaCommand = connection.CreateCommand())
        {
            pragmaCommand.CommandText = "PRAGMA table_info('AspNetUsers');";
            await using var reader = await pragmaCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var columnName = reader.GetString(1);
                if (string.Equals(columnName, "DailySummaryLastSentUtc", StringComparison.OrdinalIgnoreCase))
                {
                    hasDailySummaryColumn = true;
                }
                else if (string.Equals(columnName, "DefaultPropertyId", StringComparison.OrdinalIgnoreCase))
                {
                    hasDefaultPropertyColumn = true;
                }
            }
        }

        if (!hasDailySummaryColumn)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "ALTER TABLE \"AspNetUsers\" ADD COLUMN \"DailySummaryLastSentUtc\" TEXT NULL;";
            await command.ExecuteNonQueryAsync();
        }

        if (!hasDefaultPropertyColumn)
        {
            await using var addDefaultColumn = connection.CreateCommand();
            addDefaultColumn.CommandText = "ALTER TABLE \"AspNetUsers\" ADD COLUMN \"DefaultPropertyId\" INTEGER NULL;";
            await addDefaultColumn.ExecuteNonQueryAsync();
        }

        if (!await IndexExistsAsync(connection, "AspNetUsers", "IX_AspNetUsers_DefaultPropertyId"))
        {
            await using var createIndex = connection.CreateCommand();
            createIndex.CommandText =
                """
                CREATE INDEX IF NOT EXISTS "IX_AspNetUsers_DefaultPropertyId"
                ON "AspNetUsers" ("DefaultPropertyId");
                """;
            await createIndex.ExecuteNonQueryAsync();
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

static bool IsMissingWorkOrderTypeTableError(SqliteException ex)
    => ex.SqliteErrorCode == 1 && ex.Message.Contains("no such table: WorkOrderType", StringComparison.OrdinalIgnoreCase);

static bool IsLegacyPassOnLogSchemaError(SqliteException ex)
    => ex.SqliteErrorCode == 11 && ex.Message.Contains("PassOnLog", StringComparison.OrdinalIgnoreCase);

static bool IsDuplicateMustChangePasswordColumnError(SqliteException ex)
    => ex.SqliteErrorCode == 1
       && ex.Message.Contains("duplicate column name: MustChangePassword", StringComparison.OrdinalIgnoreCase);

static bool IsDuplicateBookmarkQuickFlagColumnError(SqliteException ex)
    => ex.SqliteErrorCode == 1
       && ex.Message.Contains("duplicate column name: ShowInQuickMenu", StringComparison.OrdinalIgnoreCase);

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
    if (dbContext.Database.GetDbConnection() is not SqliteConnection connection)
    {
        return;
    }

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
    var trimmedName = tableName.Trim('"');

    if (connection is SqliteConnection sqliteConnection)
    {
        try
        {
            await using var checkCommand = sqliteConnection.CreateCommand();
            checkCommand.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name = @name LIMIT 1;";

            var parameter = checkCommand.CreateParameter();
            parameter.ParameterName = "@name";
            parameter.Value = trimmedName;
            checkCommand.Parameters.Add(parameter);

            var result = await checkCommand.ExecuteScalarAsync();
            return result != null;
        }
        catch (SqliteException ex) when (IsDuplicateTableSchemaError(ex, trimmedName))
        {
            // Legacy databases may contain duplicate schema entries which surface as
            // "malformed database schema" errors even though the table already exists.
            // Treat this as the table being present so the legacy migration logic can continue.
            return true;
        }
    }

    if (connection is NpgsqlConnection npgsqlConnection)
    {
        await using var checkCommand = npgsqlConnection.CreateCommand();
        checkCommand.CommandText =
            """
            SELECT 1
            FROM information_schema.tables
            WHERE table_schema = current_schema() AND table_name = @name
            LIMIT 1;
            """;

        var parameter = checkCommand.CreateParameter();
        parameter.ParameterName = "@name";
        parameter.Value = trimmedName;
        checkCommand.Parameters.Add(parameter);

        var result = await checkCommand.ExecuteScalarAsync();
        return result != null;
    }

    throw new NotSupportedException($"Table existence checks are not implemented for provider '{connection.GetType().Name}'.");
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

static async Task<bool> IndexExistsAsync(DbConnection connection, string tableName, string indexName)
{
    await using var command = connection.CreateCommand();
    command.CommandText = $"PRAGMA index_list('{tableName}');";

    await using var reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        if (string.Equals(reader.GetString(1), indexName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
    }

    return false;
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
    if (dbContext.Database.GetDbConnection() is not SqliteConnection sqliteConnection)
    {
        return;
    }

    var shouldCloseConnection = sqliteConnection.State != ConnectionState.Open;

    if (shouldCloseConnection)
    {
        await sqliteConnection.OpenAsync();
    }

    try
    {
        await RemoveLegacyDuplicatePassOnLogSchemaEntriesAsync(sqliteConnection);

        var requiredTables = new[]
        {
            "PassOnLogs",
            "PassOnLogComments",
            "PassOnLogProperties",
            "PassOnLogViews"
        };

        foreach (var table in requiredTables)
        {
            if (!await TableExistsAsync(sqliteConnection, table))
            {
                return;
            }
        }

        await EnsureMigrationsHistoryTableAsync(sqliteConnection);

        const string migrationId = "20251018090000_AddPassOnLogs";

        await using var insertCommand = sqliteConnection.CreateCommand();
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
            await sqliteConnection.CloseAsync();
        }
    }
}

static async Task EnsureInitialPostgresMigrationRecordedAsync(ApplicationDbContext dbContext)
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

        await EnsureMigrationRecordedAsync(connection, "20251215210146_InitialPostgres", "8.0.20");
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

static async Task RemoveLegacyDuplicatePassOnLogSchemaEntriesAsync(SqliteConnection connection)
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




