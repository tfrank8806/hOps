using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace hOps.web.Migrations
{
    /// <inheritdoc />
    public partial class InitialSqlServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Properties",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Properties", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RoomTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserAccessRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FirstName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MobilePhone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PropertyCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsApproved = table.Column<bool>(type: "bit", nullable: false),
                    IsRejected = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAccessRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MobilePhone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProfilePhotoPath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MustChangePassword = table.Column<bool>(type: "bit", nullable: false),
                    EmailOnMessage = table.Column<bool>(type: "bit", nullable: false),
                    EmailOnMention = table.Column<bool>(type: "bit", nullable: false),
                    EmailOnWorkOrderDepartment = table.Column<bool>(type: "bit", nullable: false),
                    EmailOnLogEntry = table.Column<bool>(type: "bit", nullable: false),
                    EmailDailySummary = table.Column<bool>(type: "bit", nullable: false),
                    EmailOnSchedulePosted = table.Column<bool>(type: "bit", nullable: false),
                    DailySummaryLastSentUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DefaultPropertyId = table.Column<int>(type: "int", nullable: true),
                    TimeZoneId = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "Central Standard Time"),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUsers_Properties_DefaultPropertyId",
                        column: x => x.DefaultPropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CalendarCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Color = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PropertyId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalendarCategories_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Departments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Color = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PropertyId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Departments_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PhonebookTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Color = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PropertyId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhonebookTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PhonebookTypes_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Rooms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PropertyId = table.Column<int>(type: "int", nullable: false),
                    RoomNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Floor = table.Column<int>(type: "int", nullable: false),
                    RoomType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    X = table.Column<int>(type: "int", nullable: false),
                    Y = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rooms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rooms_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleShiftTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PropertyId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ShiftName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    EndTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    ColorHex = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AlertIfMissing = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleShiftTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduleShiftTemplates_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkOrderTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Color = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PropertyId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrderTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkOrderTypes_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Bookmarks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Url = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Section = table.Column<int>(type: "int", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PropertyId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bookmarks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bookmarks_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Bookmarks_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BulletinPosts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PropertyId = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(450)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BulletinPosts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BulletinPosts_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BulletinPosts_AspNetUsers_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BulletinPosts_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DirectMessageConversations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParticipantAId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ParticipantBId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ParticipantAArchived = table.Column<bool>(type: "bit", nullable: false),
                    ParticipantBArchived = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DirectMessageConversations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DirectMessageConversations_AspNetUsers_ParticipantAId",
                        column: x => x.ParticipantAId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DirectMessageConversations_AspNetUsers_ParticipantBId",
                        column: x => x.ParticipantBId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentFolders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ParentFolderId = table.Column<int>(type: "int", nullable: true),
                    Visibility = table.Column<int>(type: "int", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentFolders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentFolders_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DocumentFolders_DocumentFolders_ParentFolderId",
                        column: x => x.ParentFolderId,
                        principalTable: "DocumentFolders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LostFoundEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PropertyId = table.Column<int>(type: "int", nullable: false),
                    DateFound = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DateReportedLost = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FoundBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    GuestName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    GuestPhone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    GuestAddress = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    GuestEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Location = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ItemFound = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ItemLost = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    Stored = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    PhotoPath = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedByDisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    MatchedEntryId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LostFoundEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LostFoundEntries_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LostFoundEntries_LostFoundEntries_MatchedEntryId",
                        column: x => x.MatchedEntryId,
                        principalTable: "LostFoundEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LostFoundEntries_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ManagerAnnouncements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PropertyId = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManagerAnnouncements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ManagerAnnouncements_AspNetUsers_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ManagerAnnouncements_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PackageLogEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PropertyId = table.Column<int>(type: "int", nullable: false),
                    RecipientName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RoomNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Carrier = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    TrackingNumber = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    LoggedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StorageLocation = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ArrivalDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DepartureDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Delivered = table.Column<bool>(type: "bit", nullable: false),
                    DeliveredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LoggedById = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackageLogEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PackageLogEntries_AspNetUsers_LoggedById",
                        column: x => x.LoggedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PackageLogEntries_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PassOnLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PassOnLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PassOnLogs_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleEmployees",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PropertyId = table.Column<int>(type: "int", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApplicationUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    EmailAlertsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleEmployees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduleEmployees_AspNetUsers_ApplicationUserId",
                        column: x => x.ApplicationUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ScheduleEmployees_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ScheduleEmployees_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Schedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PropertyId = table.Column<int>(type: "int", nullable: false),
                    WeekStartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    WeekEndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PostedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    PostedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Schedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Schedules_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Schedules_AspNetUsers_PostedById",
                        column: x => x.PostedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Schedules_AspNetUsers_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Schedules_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PropertyId = table.Column<int>(type: "int", nullable: false),
                    StartDayOfWeek = table.Column<int>(type: "int", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduleSettings_AspNetUsers_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ScheduleSettings_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserPropertyAccesses",
                columns: table => new
                {
                    ApplicationUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PropertyId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPropertyAccesses", x => new { x.ApplicationUserId, x.PropertyId });
                    table.ForeignKey(
                        name: "FK_UserPropertyAccesses_AspNetUsers_ApplicationUserId",
                        column: x => x.ApplicationUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserPropertyAccesses_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserPropertyEmailSubscriptions",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PropertyId = table.Column<int>(type: "int", nullable: false),
                    IncludeInLogAlerts = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IncludeInDailySummary = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IncludeInWorkOrderAlerts = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPropertyEmailSubscriptions", x => new { x.UserId, x.PropertyId });
                    table.ForeignKey(
                        name: "FK_UserPropertyEmailSubscriptions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserPropertyEmailSubscriptions_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CalendarEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CalendarCategoryId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    Recurrence = table.Column<int>(type: "int", nullable: false),
                    Details = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalendarEvents_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CalendarEvents_CalendarCategories_CalendarCategoryId",
                        column: x => x.CalendarCategoryId,
                        principalTable: "CalendarCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserDepartmentSubscriptions",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DepartmentId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDepartmentSubscriptions", x => new { x.UserId, x.DepartmentId });
                    table.ForeignKey(
                        name: "FK_UserDepartmentSubscriptions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserDepartmentSubscriptions_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PhonebookContacts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TypeName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PhonebookTypeId = table.Column<int>(type: "int", nullable: true),
                    FirstName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Company = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Fax = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Website = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhonebookContacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PhonebookContacts_PhonebookTypes_PhonebookTypeId",
                        column: x => x.PhonebookTypeId,
                        principalTable: "PhonebookTypes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "RoomLayouts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PropertyId = table.Column<int>(type: "int", nullable: false),
                    RoomId = table.Column<int>(type: "int", nullable: false),
                    Floor = table.Column<int>(type: "int", nullable: false),
                    X = table.Column<int>(type: "int", nullable: false),
                    Y = table.Column<int>(type: "int", nullable: false),
                    Width = table.Column<int>(type: "int", nullable: false),
                    Height = table.Column<int>(type: "int", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ShapeType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ShapeData = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomLayouts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoomLayouts_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RoomLayouts_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkOrders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Status = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Location = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    WorkOrderTypeId = table.Column<int>(type: "int", nullable: true),
                    Issue = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Details = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    DepartmentId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkOrders_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkOrders_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkOrders_WorkOrderTypes_WorkOrderTypeId",
                        column: x => x.WorkOrderTypeId,
                        principalTable: "WorkOrderTypes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "BulletinPostAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BulletinPostId = table.Column<int>(type: "int", nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BulletinPostAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BulletinPostAttachments_BulletinPosts_BulletinPostId",
                        column: x => x.BulletinPostId,
                        principalTable: "BulletinPosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DirectMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConversationId = table.Column<int>(type: "int", nullable: false),
                    SenderId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RecipientId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DirectMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DirectMessages_AspNetUsers_RecipientId",
                        column: x => x.RecipientId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DirectMessages_AspNetUsers_SenderId",
                        column: x => x.SenderId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DirectMessages_DirectMessageConversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "DirectMessageConversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DocumentFolderProperties",
                columns: table => new
                {
                    DocumentFolderId = table.Column<int>(type: "int", nullable: false),
                    PropertyId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentFolderProperties", x => new { x.DocumentFolderId, x.PropertyId });
                    table.ForeignKey(
                        name: "FK_DocumentFolderProperties_DocumentFolders_DocumentFolderId",
                        column: x => x.DocumentFolderId,
                        principalTable: "DocumentFolders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DocumentFolderProperties_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Documents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    FilePath = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    FolderId = table.Column<int>(type: "int", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AccessScope = table.Column<int>(type: "int", nullable: false),
                    PropertyId = table.Column<int>(type: "int", nullable: true),
                    UploadedById = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UploadedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Documents_AspNetUsers_UploadedById",
                        column: x => x.UploadedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Documents_DocumentFolders_FolderId",
                        column: x => x.FolderId,
                        principalTable: "DocumentFolders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Documents_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ManagerAnnouncementAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ManagerAnnouncementId = table.Column<int>(type: "int", nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManagerAnnouncementAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ManagerAnnouncementAttachments_ManagerAnnouncements_ManagerAnnouncementId",
                        column: x => x.ManagerAnnouncementId,
                        principalTable: "ManagerAnnouncements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PassOnLogAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PassOnLogId = table.Column<int>(type: "int", nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PassOnLogAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PassOnLogAttachments_PassOnLogs_PassOnLogId",
                        column: x => x.PassOnLogId,
                        principalTable: "PassOnLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PassOnLogComments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PassOnLogId = table.Column<int>(type: "int", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PassOnLogComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PassOnLogComments_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PassOnLogComments_PassOnLogs_PassOnLogId",
                        column: x => x.PassOnLogId,
                        principalTable: "PassOnLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PassOnLogProperties",
                columns: table => new
                {
                    PassOnLogId = table.Column<int>(type: "int", nullable: false),
                    PropertyId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PassOnLogProperties", x => new { x.PassOnLogId, x.PropertyId });
                    table.ForeignKey(
                        name: "FK_PassOnLogProperties_PassOnLogs_PassOnLogId",
                        column: x => x.PassOnLogId,
                        principalTable: "PassOnLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PassOnLogProperties_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PassOnLogViews",
                columns: table => new
                {
                    PassOnLogId = table.Column<int>(type: "int", nullable: false),
                    ViewerId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ViewedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PassOnLogViews", x => new { x.PassOnLogId, x.ViewerId });
                    table.ForeignKey(
                        name: "FK_PassOnLogViews_AspNetUsers_ViewerId",
                        column: x => x.ViewerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PassOnLogViews_PassOnLogs_PassOnLogId",
                        column: x => x.PassOnLogId,
                        principalTable: "PassOnLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleTimeOffRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PropertyId = table.Column<int>(type: "int", nullable: false),
                    ScheduleEmployeeId = table.Column<int>(type: "int", nullable: false),
                    SubmittedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DecisionByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    DecisionAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecisionNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleTimeOffRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduleTimeOffRequests_AspNetUsers_DecisionByUserId",
                        column: x => x.DecisionByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ScheduleTimeOffRequests_AspNetUsers_SubmittedByUserId",
                        column: x => x.SubmittedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduleTimeOffRequests_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScheduleTimeOffRequests_ScheduleEmployees_ScheduleEmployeeId",
                        column: x => x.ScheduleEmployeeId,
                        principalTable: "ScheduleEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ScheduleId = table.Column<int>(type: "int", nullable: false),
                    ScheduleEmployeeId = table.Column<int>(type: "int", nullable: false),
                    ShiftDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ShiftName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ShiftStartTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    ShiftEndTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ColorHex = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduleAssignments_ScheduleEmployees_ScheduleEmployeeId",
                        column: x => x.ScheduleEmployeeId,
                        principalTable: "ScheduleEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScheduleAssignments_Schedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "Schedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CalendarEventProperties",
                columns: table => new
                {
                    CalendarEventId = table.Column<int>(type: "int", nullable: false),
                    PropertyId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarEventProperties", x => new { x.CalendarEventId, x.PropertyId });
                    table.ForeignKey(
                        name: "FK_CalendarEventProperties_CalendarEvents_CalendarEventId",
                        column: x => x.CalendarEventId,
                        principalTable: "CalendarEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CalendarEventProperties_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserToDoItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WorkOrderId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserToDoItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserToDoItems_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserToDoItems_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "WorkOrderAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkOrderId = table.Column<int>(type: "int", nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrderAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkOrderAttachments_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkOrderProperties",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkOrderId = table.Column<int>(type: "int", nullable: false),
                    PropertyId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrderProperties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkOrderProperties_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkOrderProperties_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserNotifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LinkUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DirectMessageId = table.Column<int>(type: "int", nullable: true),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserNotifications_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserNotifications_DirectMessages_DirectMessageId",
                        column: x => x.DirectMessageId,
                        principalTable: "DirectMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DocumentProperties",
                columns: table => new
                {
                    DocumentId = table.Column<int>(type: "int", nullable: false),
                    PropertyId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentProperties", x => new { x.DocumentId, x.PropertyId });
                    table.ForeignKey(
                        name: "FK_DocumentProperties_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DocumentProperties_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_DefaultPropertyId",
                table: "AspNetUsers",
                column: "DefaultPropertyId");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Bookmarks_CreatedById",
                table: "Bookmarks",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Bookmarks_PropertyId",
                table: "Bookmarks",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_BulletinPostAttachments_BulletinPostId",
                table: "BulletinPostAttachments",
                column: "BulletinPostId");

            migrationBuilder.CreateIndex(
                name: "IX_BulletinPosts_CreatedById",
                table: "BulletinPosts",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_BulletinPosts_PropertyId",
                table: "BulletinPosts",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_BulletinPosts_UpdatedById",
                table: "BulletinPosts",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarCategories_PropertyId",
                table: "CalendarCategories",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEventProperties_PropertyId",
                table: "CalendarEventProperties",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_CalendarCategoryId",
                table: "CalendarEvents",
                column: "CalendarCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_CreatedById",
                table: "CalendarEvents",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_PropertyId",
                table: "Departments",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_DirectMessageConversations_ParticipantAId_ParticipantBId",
                table: "DirectMessageConversations",
                columns: new[] { "ParticipantAId", "ParticipantBId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DirectMessageConversations_ParticipantBId",
                table: "DirectMessageConversations",
                column: "ParticipantBId");

            migrationBuilder.CreateIndex(
                name: "IX_DirectMessages_ConversationId",
                table: "DirectMessages",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_DirectMessages_RecipientId",
                table: "DirectMessages",
                column: "RecipientId");

            migrationBuilder.CreateIndex(
                name: "IX_DirectMessages_SenderId",
                table: "DirectMessages",
                column: "SenderId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentFolderProperties_PropertyId",
                table: "DocumentFolderProperties",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentFolders_CreatedById",
                table: "DocumentFolders",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentFolders_ParentFolderId",
                table: "DocumentFolders",
                column: "ParentFolderId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentProperties_PropertyId",
                table: "DocumentProperties",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_FolderId",
                table: "Documents",
                column: "FolderId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_PropertyId",
                table: "Documents",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_UploadedById",
                table: "Documents",
                column: "UploadedById");

            migrationBuilder.CreateIndex(
                name: "IX_LostFoundEntries_CreatedByUserId",
                table: "LostFoundEntries",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LostFoundEntries_MatchedEntryId",
                table: "LostFoundEntries",
                column: "MatchedEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_LostFoundEntries_PropertyId",
                table: "LostFoundEntries",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_ManagerAnnouncementAttachments_ManagerAnnouncementId",
                table: "ManagerAnnouncementAttachments",
                column: "ManagerAnnouncementId");

            migrationBuilder.CreateIndex(
                name: "IX_ManagerAnnouncements_PropertyId",
                table: "ManagerAnnouncements",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_ManagerAnnouncements_UpdatedById",
                table: "ManagerAnnouncements",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_PackageLogEntries_LoggedById",
                table: "PackageLogEntries",
                column: "LoggedById");

            migrationBuilder.CreateIndex(
                name: "IX_PackageLogEntries_PropertyId",
                table: "PackageLogEntries",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_PassOnLogAttachments_PassOnLogId",
                table: "PassOnLogAttachments",
                column: "PassOnLogId");

            migrationBuilder.CreateIndex(
                name: "IX_PassOnLogComments_CreatedById",
                table: "PassOnLogComments",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_PassOnLogComments_PassOnLogId",
                table: "PassOnLogComments",
                column: "PassOnLogId");

            migrationBuilder.CreateIndex(
                name: "IX_PassOnLogProperties_PropertyId",
                table: "PassOnLogProperties",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_PassOnLogs_CreatedById",
                table: "PassOnLogs",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_PassOnLogViews_ViewerId",
                table: "PassOnLogViews",
                column: "ViewerId");

            migrationBuilder.CreateIndex(
                name: "IX_PhonebookContacts_PhonebookTypeId",
                table: "PhonebookContacts",
                column: "PhonebookTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_PhonebookTypes_PropertyId",
                table: "PhonebookTypes",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_RoomLayouts_PropertyId",
                table: "RoomLayouts",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_RoomLayouts_RoomId",
                table: "RoomLayouts",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_PropertyId",
                table: "Rooms",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleAssignments_ScheduleEmployeeId",
                table: "ScheduleAssignments",
                column: "ScheduleEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleAssignments_ScheduleId",
                table: "ScheduleAssignments",
                column: "ScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleEmployees_ApplicationUserId",
                table: "ScheduleEmployees",
                column: "ApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleEmployees_CreatedByUserId",
                table: "ScheduleEmployees",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleEmployees_PropertyId_ApplicationUserId",
                table: "ScheduleEmployees",
                columns: new[] { "PropertyId", "ApplicationUserId" },
                unique: true,
                filter: "[ApplicationUserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Schedules_CreatedById",
                table: "Schedules",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Schedules_PostedById",
                table: "Schedules",
                column: "PostedById");

            migrationBuilder.CreateIndex(
                name: "IX_Schedules_PropertyId",
                table: "Schedules",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_Schedules_UpdatedById",
                table: "Schedules",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleSettings_PropertyId",
                table: "ScheduleSettings",
                column: "PropertyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleSettings_UpdatedByUserId",
                table: "ScheduleSettings",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleShiftTemplates_PropertyId_SortOrder",
                table: "ScheduleShiftTemplates",
                columns: new[] { "PropertyId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleTimeOffRequests_DecisionByUserId",
                table: "ScheduleTimeOffRequests",
                column: "DecisionByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleTimeOffRequests_PropertyId",
                table: "ScheduleTimeOffRequests",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleTimeOffRequests_ScheduleEmployeeId",
                table: "ScheduleTimeOffRequests",
                column: "ScheduleEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleTimeOffRequests_SubmittedByUserId",
                table: "ScheduleTimeOffRequests",
                column: "SubmittedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserDepartmentSubscriptions_DepartmentId",
                table: "UserDepartmentSubscriptions",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_UserNotifications_DirectMessageId",
                table: "UserNotifications",
                column: "DirectMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_UserNotifications_UserId",
                table: "UserNotifications",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPropertyAccesses_PropertyId",
                table: "UserPropertyAccesses",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPropertyEmailSubscriptions_PropertyId",
                table: "UserPropertyEmailSubscriptions",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_UserToDoItems_UserId",
                table: "UserToDoItems",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserToDoItems_WorkOrderId",
                table: "UserToDoItems",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderAttachments_WorkOrderId",
                table: "WorkOrderAttachments",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderProperties_PropertyId",
                table: "WorkOrderProperties",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderProperties_WorkOrderId",
                table: "WorkOrderProperties",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_CreatedById",
                table: "WorkOrders",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_DepartmentId",
                table: "WorkOrders",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_WorkOrderTypeId",
                table: "WorkOrders",
                column: "WorkOrderTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderTypes_PropertyId",
                table: "WorkOrderTypes",
                column: "PropertyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "Bookmarks");

            migrationBuilder.DropTable(
                name: "BulletinPostAttachments");

            migrationBuilder.DropTable(
                name: "CalendarEventProperties");

            migrationBuilder.DropTable(
                name: "DocumentFolderProperties");

            migrationBuilder.DropTable(
                name: "DocumentProperties");

            migrationBuilder.DropTable(
                name: "LostFoundEntries");

            migrationBuilder.DropTable(
                name: "ManagerAnnouncementAttachments");

            migrationBuilder.DropTable(
                name: "PackageLogEntries");

            migrationBuilder.DropTable(
                name: "PassOnLogAttachments");

            migrationBuilder.DropTable(
                name: "PassOnLogComments");

            migrationBuilder.DropTable(
                name: "PassOnLogProperties");

            migrationBuilder.DropTable(
                name: "PassOnLogViews");

            migrationBuilder.DropTable(
                name: "PhonebookContacts");

            migrationBuilder.DropTable(
                name: "RoomLayouts");

            migrationBuilder.DropTable(
                name: "RoomTypes");

            migrationBuilder.DropTable(
                name: "ScheduleAssignments");

            migrationBuilder.DropTable(
                name: "ScheduleSettings");

            migrationBuilder.DropTable(
                name: "ScheduleShiftTemplates");

            migrationBuilder.DropTable(
                name: "ScheduleTimeOffRequests");

            migrationBuilder.DropTable(
                name: "UserAccessRequests");

            migrationBuilder.DropTable(
                name: "UserDepartmentSubscriptions");

            migrationBuilder.DropTable(
                name: "UserNotifications");

            migrationBuilder.DropTable(
                name: "UserPropertyAccesses");

            migrationBuilder.DropTable(
                name: "UserPropertyEmailSubscriptions");

            migrationBuilder.DropTable(
                name: "UserToDoItems");

            migrationBuilder.DropTable(
                name: "WorkOrderAttachments");

            migrationBuilder.DropTable(
                name: "WorkOrderProperties");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "BulletinPosts");

            migrationBuilder.DropTable(
                name: "CalendarEvents");

            migrationBuilder.DropTable(
                name: "Documents");

            migrationBuilder.DropTable(
                name: "ManagerAnnouncements");

            migrationBuilder.DropTable(
                name: "PassOnLogs");

            migrationBuilder.DropTable(
                name: "PhonebookTypes");

            migrationBuilder.DropTable(
                name: "Rooms");

            migrationBuilder.DropTable(
                name: "Schedules");

            migrationBuilder.DropTable(
                name: "ScheduleEmployees");

            migrationBuilder.DropTable(
                name: "DirectMessages");

            migrationBuilder.DropTable(
                name: "WorkOrders");

            migrationBuilder.DropTable(
                name: "CalendarCategories");

            migrationBuilder.DropTable(
                name: "DocumentFolders");

            migrationBuilder.DropTable(
                name: "DirectMessageConversations");

            migrationBuilder.DropTable(
                name: "Departments");

            migrationBuilder.DropTable(
                name: "WorkOrderTypes");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "Properties");
        }
    }
}
