using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using hOps.web.Data;
using hOps.web.Models;
using hOps.web.Services;
using hOps.web.Services.Localization;
using hOps.web.Utilities;
using hOps.web.ViewModels;
using hOps.web.ViewModels.Home;
using hOps.web.ViewModels.WorkOrders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace hOps.web.Controllers
{
    [Authorize]
    public class HomeController : BaseController
    {
        private readonly MentionService _mentionService;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<HomeController> _logger;
        private readonly IConfiguration _configuration;
        private readonly ITranslationService _translationService;

        private const string PersonaSessionKey = "HomeLayoutPersona";
        private const int DefaultWidgetHeight = 300;
        private const int MinWidgetHeight = 220;
        private const int MaxWidgetHeight = 1500;
        private const int WidgetHeightStep = 10;
        private const int WidgetHeightResetThreshold = 6;

        private static readonly IReadOnlyList<HomeWidgetDefinition> WidgetDefinitions = new[]
        {
            new HomeWidgetDefinition { Id = HomeWidgetIds.Announcements, DisplayName = "Announcements", Description = "Manager notes & attachments", DefaultSize = HomeWidgetSize.Third, DefaultHeight = 690 },
            new HomeWidgetDefinition { Id = HomeWidgetIds.Bulletins, DisplayName = "Bulletin Board", Description = "Team conversations & reminders", DefaultSize = HomeWidgetSize.Third, DefaultHeight = 690 },
            new HomeWidgetDefinition { Id = HomeWidgetIds.WorkOrders, DisplayName = "Work Orders", Description = "Active tickets and SLAs", DefaultSize = HomeWidgetSize.Third, DefaultHeight = 690 },
            new HomeWidgetDefinition { Id = HomeWidgetIds.UpcomingEvents, DisplayName = "Upcoming Events", Description = "Calendar highlights", DefaultSize = HomeWidgetSize.Third, DefaultHeight = 490 },
            new HomeWidgetDefinition { Id = HomeWidgetIds.CalendarMonth, DisplayName = "Calendar Month", Description = "Month view highlighting event days", DefaultSize = HomeWidgetSize.Third, DefaultHeight = 420 },
            new HomeWidgetDefinition { Id = HomeWidgetIds.PassOnLogs, DisplayName = "Pass On Logs", Description = "Recent pass on entries", DefaultSize = HomeWidgetSize.Full, DefaultHeight = 490, DefaultSpanOverride = 8 },
            new HomeWidgetDefinition { Id = HomeWidgetIds.MySchedule, DisplayName = "My Schedule", Description = "Upcoming shifts for you", DefaultSize = HomeWidgetSize.Third, DefaultHeight = 300 },
            new HomeWidgetDefinition { Id = HomeWidgetIds.LostFound, DisplayName = "Lost & Found", Description = "Items awaiting resolution", DefaultSize = HomeWidgetSize.Third, DefaultHeight = 300 },
            new HomeWidgetDefinition { Id = HomeWidgetIds.PackageLog, DisplayName = "Package Log", Description = "Undelivered packages", DefaultSize = HomeWidgetSize.Third, DefaultHeight = 300 },
            new HomeWidgetDefinition { Id = HomeWidgetIds.HotelLayout, DisplayName = "Hotel Layout", Description = "Interactive property map", DefaultSize = HomeWidgetSize.Full, DefaultHeight = 870 },
            new HomeWidgetDefinition { Id = HomeWidgetIds.OpsFeed, DisplayName = "Ops Feed", Description = "Unified activity and replies", DefaultSize = HomeWidgetSize.Full, DefaultHeight = 750 }
        };

        private sealed record DefaultWidgetLayoutSpec(string WidgetId, HomeWidgetSize Size, int? CustomSpan = null, int? CustomHeight = null);

        private static readonly IReadOnlyList<DefaultWidgetLayoutSpec> DefaultWidgetLayout = new[]
        {
            new DefaultWidgetLayoutSpec(HomeWidgetIds.Announcements, HomeWidgetSize.Third),
            new DefaultWidgetLayoutSpec(HomeWidgetIds.Bulletins, HomeWidgetSize.Third),
            new DefaultWidgetLayoutSpec(HomeWidgetIds.WorkOrders, HomeWidgetSize.Third),
            new DefaultWidgetLayoutSpec(HomeWidgetIds.UpcomingEvents, HomeWidgetSize.Third),
            new DefaultWidgetLayoutSpec(HomeWidgetIds.CalendarMonth, HomeWidgetSize.Third),
            new DefaultWidgetLayoutSpec(HomeWidgetIds.PassOnLogs, HomeWidgetSize.Full, CustomSpan: 8),
            new DefaultWidgetLayoutSpec(HomeWidgetIds.MySchedule, HomeWidgetSize.Third),
            new DefaultWidgetLayoutSpec(HomeWidgetIds.LostFound, HomeWidgetSize.Third),
            new DefaultWidgetLayoutSpec(HomeWidgetIds.PackageLog, HomeWidgetSize.Third),
            new DefaultWidgetLayoutSpec(HomeWidgetIds.HotelLayout, HomeWidgetSize.Full),
            new DefaultWidgetLayoutSpec(HomeWidgetIds.OpsFeed, HomeWidgetSize.Full)
        };

        private static readonly LayoutPersonaOption[] PersonaOptions = new[]
        {
            new LayoutPersonaOption("default", "Standard", "Balanced dashboard for everyone"),
            new LayoutPersonaOption("frontDesk", "Front Desk", "Front-of-house focus"),
            new LayoutPersonaOption("engineering", "Engineering", "Maintenance & SLAs"),
            new LayoutPersonaOption("housekeeping", "Housekeeping", "Rooms, lost & found, packages")
        };

        private static readonly Dictionary<HomeWidgetSize, string> WidgetSizeClasses = new()
        {
            [HomeWidgetSize.Full] = "col-12",
            [HomeWidgetSize.Half] = "col-12 col-xl-6",
            [HomeWidgetSize.Third] = "col-12 col-lg-6 col-xl-4",
            [HomeWidgetSize.Quarter] = "col-12 col-md-6 col-xl-3"
        };

        private static readonly JsonSerializerOptions LayoutSerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        public HomeController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            MentionService mentionService,
            IWebHostEnvironment environment,
            IConfiguration configuration,
            ILogger<HomeController> logger,
            ITranslationService translationService)
            : base(context, userManager)
        {
            _mentionService = mentionService;
            _environment = environment;
            _logger = logger;
            _configuration = configuration;
            _translationService = translationService;
        }

        public async Task<IActionResult> Index()
        {
            var activeLanguage = HttpContext.Items["ActiveLanguage"] as string ?? "(null)";
            var layoutLanguage = ViewBag?.ActiveLanguage as string ?? "(null)";
            _logger.LogInformation(
                "LANGDEBUG Home/Index culture={Culture} uiCulture={UICulture} active={ActiveLanguage} defaultLang={DefaultLanguage} staticLang={StaticLanguage}",
                CultureInfo.CurrentCulture.Name,
                CultureInfo.CurrentUICulture.Name,
                activeLanguage,
                _translationService.DefaultLanguage,
                layoutLanguage);

            ViewData["Title"] = "Home";

            var viewModel = new HomeIndexViewModel();
            var currentProperty = ViewBag.CurrentProperty as Property;
            viewModel.CurrentProperty = currentProperty;

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return View(viewModel);
            }

            var personaKey = ResolvePersonaKey(Request.Query["persona"]);
            viewModel.SelectedPersona = personaKey;
            viewModel.LayoutPersonas = PersonaOptions
                .Select(option => new LayoutPersonaViewModel
                {
                    Key = option.Key,
                    Name = option.Name,
                    Description = option.Description,
                    IsSelected = option.Key.Equals(personaKey, StringComparison.OrdinalIgnoreCase)
                })
                .ToList();

            var activeWidgetDefinitions = (await GetActiveWidgetDefinitionsAsync()).ToList();

            viewModel.ActiveWidgetDefinitions = activeWidgetDefinitions;
            viewModel.WidgetHeightDefault = DefaultWidgetHeight;
            viewModel.WidgetHeightMin = MinWidgetHeight;
            viewModel.WidgetHeightMax = MaxWidgetHeight;
            viewModel.WidgetHeightStep = WidgetHeightStep;
            viewModel.WidgetHeightResetThreshold = WidgetHeightResetThreshold;

            viewModel.WidgetLayout = await BuildWidgetLayoutAsync(user.Id, personaKey, activeWidgetDefinitions);
            viewModel.WidgetSizeClasses = WidgetSizeClasses.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            var userRoles = await _userManager.GetRolesAsync(user);
            var roleSet = new HashSet<string>(userRoles, StringComparer.OrdinalIgnoreCase);
            var canManage = roleSet.Contains("Admin") || roleSet.Contains("Manager");
            viewModel.CanManageAnnouncements = canManage;
            viewModel.CanManageWidgets = canManage;
            viewModel.MarketplaceModules = await BuildMarketplaceViewModelAsync(activeWidgetDefinitions);

            if (currentProperty == null)
            {
                return View(viewModel);
            }

            var propertyId = currentProperty.Id;
            await PopulateAnnouncementAsync(viewModel, propertyId);
            await PopulateBulletinAsync(viewModel, propertyId, user, roleSet);
            await PopulateRoomTilesAsync(viewModel, propertyId);
            await PopulateWorkOrdersAsync(viewModel, propertyId);
            await PopulateLostFoundAsync(viewModel, propertyId);
            await PopulatePassOnLogsAsync(viewModel, propertyId, user.Id);
            await PopulatePackageLogAsync(viewModel, propertyId);
            await PopulateUpcomingEventsAsync(viewModel, propertyId);
            await PopulateCalendarMonthAsync(viewModel, propertyId);
            await PopulateQuickWorkOrderOptionsAsync(viewModel, propertyId);
            await PopulateActivityFeedAsync(viewModel, propertyId, user.Id);
            await PopulateMyScheduleAsync(viewModel, propertyId, user.Id);

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> HotelLayoutWidget(string? mode = null)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            var property = ViewBag.CurrentProperty as Property;
            var viewModel = await BuildHotelLayoutViewModelAsync(property);

            if (string.Equals(mode, "popup", StringComparison.OrdinalIgnoreCase))
            {
                return View("HotelLayoutPopup", viewModel);
            }

            if (string.Equals(mode, "panel", StringComparison.OrdinalIgnoreCase))
            {
                return View("HotelLayoutPanel", viewModel);
            }

            return PartialView("~/Views/Home/Widgets/_HotelLayoutWidget.cshtml", viewModel);
        }

        [HttpGet]
        public IActionResult SwitchProperty(int propertyId, string? returnUrl)
        {
            if (propertyId <= 0)
            {
                return BadRequest();
            }

            HttpContext.Session.SetInt32("CurrentPropertyId", propertyId);

            string? target = returnUrl;
            if (string.IsNullOrWhiteSpace(target))
            {
                var referer = Request.Headers["Referer"].ToString();
                if (!string.IsNullOrWhiteSpace(referer) &&
                    Uri.TryCreate(referer, UriKind.Absolute, out var refererUri) &&
                    string.Equals(refererUri.Host, Request.Host.Host, StringComparison.OrdinalIgnoreCase))
                {
                    target = refererUri.PathAndQuery;
                }
            }

            if (!IsSafeReturnUrl(target))
            {
                target = Url.Action(nameof(Index)) ?? "/";
            }

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new { success = true, returnUrl = target });
            }

            return Url.IsLocalUrl(target)
                ? Redirect(target)
                : RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public async Task<IActionResult> SaveWidgetLayout([FromBody] UpdateHomeLayoutRequest request)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var currentUserId = currentUser?.Id ?? HttpContext.Session.GetString("CurrentUserId");

            if (request?.Widgets == null || request.Widgets.Count == 0)
            {
                return BadRequest(new { message = "No widgets were provided." });
            }

            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                return Unauthorized(new { message = "Your session expired. Please refresh the page and sign in again before saving the layout." });
            }

            var personaKey = ResolvePersonaKey(request.Persona);
            var activeDefinitions = await GetActiveWidgetDefinitionsAsync();
            var definitions = activeDefinitions.ToDictionary(d => d.Id, d => d, StringComparer.OrdinalIgnoreCase);
            var normalized = new List<HomeWidgetLayoutEntry>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var widget in request.Widgets)
            {
                if (string.IsNullOrWhiteSpace(widget.WidgetId) || !definitions.TryGetValue(widget.WidgetId, out var definition))
                {
                    continue;
                }

                if (!Enum.TryParse(widget.Size, true, out HomeWidgetSize parsedSize))
                {
                    parsedSize = definition.DefaultSize;
                }

                var requestedSpan = widget.CustomSpan;
                int? normalizedSpan = null;
                if (requestedSpan.HasValue)
                {
                    normalizedSpan = ClampSpan(requestedSpan.Value);
                }

                var isFullWidthLocked = string.Equals(widget.WidgetId, HomeWidgetIds.HotelLayout, StringComparison.OrdinalIgnoreCase);
                if (isFullWidthLocked)
                {
                    normalizedSpan = GetSpanForSize(HomeWidgetSize.Full);
                }
                else if (normalizedSpan.HasValue)
                {
                    var defaultSpan = GetDefaultSpanForWidget(definition, parsedSize);
                    if (normalizedSpan.Value == defaultSpan)
                    {
                        normalizedSpan = null;
                    }
                }

                var requestedHeight = widget.CustomHeight;
                int? normalizedHeight = null;
                if (requestedHeight.HasValue)
                {
                    var clamped = ClampHeight(requestedHeight.Value);
                    var defaultHeight = definition.DefaultHeight > 0 ? definition.DefaultHeight : DefaultWidgetHeight;
                    if (Math.Abs(clamped - defaultHeight) > WidgetHeightResetThreshold)
                    {
                        normalizedHeight = clamped;
                    }
                }

                var spanForColumn = normalizedSpan ?? GetDefaultSpanForWidget(definition, parsedSize);
                int? normalizedColumn = null;
                if (isFullWidthLocked)
                {
                    normalizedColumn = 1;
                }
                else if (widget.ColumnStart.HasValue)
                {
                    normalizedColumn = ClampColumnStart(widget.ColumnStart.Value, spanForColumn);
                }

                int? normalizedRow = null;
                if (widget.RowStart.HasValue)
                {
                    normalizedRow = ClampRowStart(widget.RowStart.Value);
                }

                if (!seen.Add(widget.WidgetId))
                {
                    continue;
                }

                normalized.Add(new HomeWidgetLayoutEntry
                {
                    WidgetId = widget.WidgetId,
                    Size = parsedSize,
                    CustomSpan = normalizedSpan,
                    CustomHeight = normalizedHeight,
                    ColumnStart = normalizedColumn,
                    RowStart = normalizedRow
                });
            }

            foreach (var definition in activeDefinitions)
            {
                if (seen.Add(definition.Id))
                {
                    normalized.Add(new HomeWidgetLayoutEntry
                    {
                        WidgetId = definition.Id,
                        Size = definition.DefaultSize
                    });
                }
            }

            var serialized = JsonSerializer.Serialize(normalized, LayoutSerializerOptions);
            var layouts = await _context.UserHomeLayouts
                .Where(l => l.UserId == currentUserId && l.PersonaKey == personaKey)
                .OrderByDescending(l => l.UpdatedAtUtc)
                .ToListAsync();
            var layout = layouts.FirstOrDefault();
            var staleLayouts = layouts.Skip(1).ToList();
            if (layout == null)
            {
                layout = new UserHomeLayout
                {
                    UserId = currentUserId,
                    PersonaKey = personaKey,
                    LayoutJson = serialized,
                    UpdatedAtUtc = DateTime.UtcNow
                };
                _context.UserHomeLayouts.Add(layout);
            }
            else
            {
                layout.PersonaKey = personaKey;
                layout.LayoutJson = serialized;
                layout.UpdatedAtUtc = DateTime.UtcNow;
            }
            layout.IsDefault = personaKey.Equals(PersonaOptions[0].Key, StringComparison.OrdinalIgnoreCase);

            if (staleLayouts.Count > 0)
            {
                _context.UserHomeLayouts.RemoveRange(staleLayouts);
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        [Authorize(Roles = "Admin,Manager")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateWidgets(WidgetMarketplaceForm form)
        {
            await EnsureMarketplaceSeedAsync();
            var enabledSet = form?.WidgetIds != null
                ? new HashSet<string>(form.WidgetIds.Where(x => !string.IsNullOrWhiteSpace(x)), StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (enabledSet.Count == 0)
            {
                enabledSet.UnionWith(WidgetDefinitions.Select(d => d.Id));
            }

            var modules = await _context.WidgetMarketplaceModules.ToListAsync();
            foreach (var module in modules)
            {
                module.IsEnabled = enabledSet.Contains(module.WidgetId);
                module.UpdatedAtUtc = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            TempData["HomeMessage"] = "Widget marketplace updated.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Manager")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateAnnouncement(ManagerAnnouncementForm form)
        {
            if (!ModelState.IsValid)
            {
                TempData["HomeError"] = "Unable to save announcement. Please ensure all required fields are filled out.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            if (!await UserHasAccessToProperty(form.PropertyId, user))
            {
                return Forbid();
            }

            var property = await _context.Properties
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == form.PropertyId);

            var announcement = await _context.ManagerAnnouncements
                .Include(a => a.Attachments)
                .FirstOrDefaultAsync(a => a.PropertyId == form.PropertyId);

            var savedAnnouncementFiles = await SaveFilesAsync(form.Files, "announcements");
            var newAnnouncementAttachments = savedAnnouncementFiles
                .Select(file => new ManagerAnnouncementAttachment
                {
                    FilePath = file.RelativePath,
                    OriginalFileName = file.OriginalFileName
                })
                .ToList();
            var removedAnnouncementPaths = new List<string>();

            if (announcement == null)
            {
                announcement = new ManagerAnnouncement
                {
                    PropertyId = form.PropertyId,
                    Content = form.Content.Trim(),
                    UpdatedById = user.Id,
                    UpdatedAt = DateTime.UtcNow,
                };

                foreach (var attachment in newAnnouncementAttachments)
                {
                    announcement.Attachments.Add(attachment);
                }

                _context.ManagerAnnouncements.Add(announcement);
            }
            else
            {
                announcement.Content = form.Content.Trim();
                announcement.UpdatedById = user.Id;
                announcement.UpdatedAt = DateTime.UtcNow;

                if (form.AttachmentsToDelete?.Any() == true)
                {
                    var toRemove = announcement.Attachments
                        .Where(a => form.AttachmentsToDelete.Contains(a.Id))
                        .ToList();

                    foreach (var attachment in toRemove)
                    {
                        removedAnnouncementPaths.Add(ResolvePhysicalPath(attachment.FilePath));
                        announcement.Attachments.Remove(attachment);
                        _context.ManagerAnnouncementAttachments.Remove(attachment);
                    }
                }

                foreach (var attachment in newAnnouncementAttachments)
                {
                    announcement.Attachments.Add(attachment);
                }
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to update announcement for property {PropertyId}", form.PropertyId);
                foreach (var file in savedAnnouncementFiles)
                {
                    DeleteFileIfExists(file.PhysicalPath);
                }

                throw;
            }

            foreach (var path in removedAnnouncementPaths)
            {
                DeleteFileIfExists(path);
            }

            await _mentionService.CreateMentionNotificationsAsync(
                announcement.Content,
                user,
                $"Announcement at {property?.Name ?? "your property"}",
                Url.Action(nameof(Index), "Home", null, Request.Scheme) ?? "/",
                announcement.Content);

            TempData["HomeMessage"] = "Manager announcement updated.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateBulletin(BulletinPostForm form)
        {
            if (!ModelState.IsValid)
            {
                TempData["HomeError"] = "Unable to add bulletin entry. Please ensure the message is provided.";
                TempData["ShowBulletinForm"] = true;
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            if (!await UserHasAccessToProperty(form.PropertyId, user))
            {
                return Forbid();
            }

            var property = await _context.Properties
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == form.PropertyId);

            var post = new BulletinPost
            {
                PropertyId = form.PropertyId,
                Content = form.Content.Trim(),
                CreatedAt = DateTime.UtcNow,
                CreatedById = user.Id,
            };

            var savedFiles = await SaveFilesAsync(form.Files, "bulletins");
            foreach (var upload in savedFiles)
            {
                post.Attachments.Add(new BulletinPostAttachment
                {
                    FilePath = upload.RelativePath,
                    OriginalFileName = upload.OriginalFileName
                });
            }

            _context.BulletinPosts.Add(post);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch
            {
                foreach (var upload in savedFiles)
                {
                    DeleteFileIfExists(upload.PhysicalPath);
                }
                throw;
            }

            await _mentionService.CreateMentionNotificationsAsync(
                post.Content,
                user,
                $"Bulletin at {property?.Name ?? "your property"}",
                Url.Action(nameof(Index), "Home", null, Request.Scheme) ?? "/",
                post.Content);

            TempData["HomeMessage"] = "Bulletin item added.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateBulletin(BulletinPostForm form)
        {
            if (!ModelState.IsValid || !form.Id.HasValue)
            {
                TempData["HomeError"] = "Unable to update the bulletin entry.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            if (!await UserHasAccessToProperty(form.PropertyId, user))
            {
                return Forbid();
            }

            var property = await _context.Properties
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == form.PropertyId);

            var post = await _context.BulletinPosts
                .Include(p => p.Attachments)
                .FirstOrDefaultAsync(p => p.Id == form.Id.Value && p.PropertyId == form.PropertyId);

            if (post == null)
            {
                return NotFound();
            }

            var roles = await _userManager.GetRolesAsync(user);
            var roleSet = new HashSet<string>(roles, StringComparer.OrdinalIgnoreCase);
            var canEdit = post.CreatedById == user.Id || roleSet.Contains("Admin") || roleSet.Contains("Manager");

            if (!canEdit)
            {
                return Forbid();
            }

            post.Content = form.Content.Trim();
            post.UpdatedAt = DateTime.UtcNow;
            post.UpdatedById = user.Id;

            var savedFiles = await SaveFilesAsync(form.Files, "bulletins");
            foreach (var upload in savedFiles)
            {
                post.Attachments.Add(new BulletinPostAttachment
                {
                    FilePath = upload.RelativePath,
                    OriginalFileName = upload.OriginalFileName
                });
            }

            var removedPaths = new List<string>();
            if (form.AttachmentsToDelete?.Any() == true)
            {
                var toRemove = post.Attachments
                    .Where(a => form.AttachmentsToDelete.Contains(a.Id))
                    .ToList();

                foreach (var attachment in toRemove)
                {
                    removedPaths.Add(ResolvePhysicalPath(attachment.FilePath));
                    post.Attachments.Remove(attachment);
                    _context.BulletinPostAttachments.Remove(attachment);
                }
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch
            {
                foreach (var upload in savedFiles)
                {
                    DeleteFileIfExists(upload.PhysicalPath);
                }
                throw;
            }

            foreach (var path in removedPaths)
            {
                DeleteFileIfExists(path);
            }

            await _mentionService.CreateMentionNotificationsAsync(
                post.Content,
                user,
                $"Bulletin at {property?.Name ?? "your property"}",
                Url.Action(nameof(Index), "Home", null, Request.Scheme) ?? "/",
                post.Content);
            TempData["HomeMessage"] = "Bulletin entry updated.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteBulletin(int id, int propertyId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            if (!await UserHasAccessToProperty(propertyId, user))
            {
                return Forbid();
            }

            var post = await _context.BulletinPosts
                .Include(p => p.Attachments)
                .FirstOrDefaultAsync(p => p.Id == id && p.PropertyId == propertyId);

            if (post == null)
            {
                return NotFound();
            }

            var roles = await _userManager.GetRolesAsync(user);
            var roleSet = new HashSet<string>(roles, StringComparer.OrdinalIgnoreCase);
            var canEdit = post.CreatedById == user.Id || roleSet.Contains("Admin") || roleSet.Contains("Manager");

            if (!canEdit)
            {
                return Forbid();
            }

            var attachmentPaths = post.Attachments
                .Select(a => ResolvePhysicalPath(a.FilePath))
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .ToList();

            _context.BulletinPosts.Remove(post);
            await _context.SaveChangesAsync();

            foreach (var path in attachmentPaths)
            {
                DeleteFileIfExists(path);
            }

            TempData["HomeMessage"] = "Bulletin entry removed.";
            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateAnnouncementAsync(HomeIndexViewModel viewModel, int propertyId)
        {
            var announcement = await _context.ManagerAnnouncements
                .Include(a => a.UpdatedBy)
                .Include(a => a.Attachments)
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.PropertyId == propertyId);

            if (announcement == null)
            {
                return;
            }

            viewModel.Announcement = new ManagerAnnouncementViewModel
            {
                Id = announcement.Id,
                Content = announcement.Content,
                UpdatedAt = announcement.UpdatedAt,
                UpdatedByName = BuildDisplayName(announcement.UpdatedBy),
                Attachments = announcement.Attachments
                    .OrderBy(a => BuildAttachmentDisplayName(a.OriginalFileName, a.FilePath), StringComparer.OrdinalIgnoreCase)
                    .Select(a => new HomeAttachmentViewModel
                    {
                        Id = a.Id,
                        FileName = BuildAttachmentDisplayName(a.OriginalFileName, a.FilePath),
                        DownloadUrl = a.FilePath
                    })
                    .ToList()
            };
        }
        private async Task PopulateBulletinAsync(HomeIndexViewModel viewModel, int propertyId, ApplicationUser currentUser, HashSet<string> roleSet)
        {
            await CleanupExpiredBulletinPostsAsync(propertyId);

            var posts = await _context.BulletinPosts
                .Where(p => p.PropertyId == propertyId)
                .Include(p => p.CreatedBy)
                .Include(p => p.UpdatedBy)
                .Include(p => p.Attachments)
                .OrderByDescending(p => p.UpdatedAt ?? p.CreatedAt)
                .AsNoTracking()
                .ToListAsync();

            var canManageAll = roleSet.Contains("Admin") || roleSet.Contains("Manager");

            viewModel.BulletinPosts = posts
                .Select(p => new BulletinPostViewModel
                {
                    Id = p.Id,
                    Content = p.Content,
                    CreatedAt = p.CreatedAt,
                    CreatedByName = BuildDisplayName(p.CreatedBy),
                    UpdatedAt = p.UpdatedAt,
                    UpdatedByName = BuildDisplayName(p.UpdatedBy),
                    CanEdit = canManageAll || p.CreatedById == currentUser.Id,
                    Attachments = p.Attachments
                        .OrderBy(a => BuildAttachmentDisplayName(a.OriginalFileName, a.FilePath), StringComparer.OrdinalIgnoreCase)
                        .Select(a => new HomeAttachmentViewModel
                        {
                            Id = a.Id,
                            FileName = BuildAttachmentDisplayName(a.OriginalFileName, a.FilePath),
                            DownloadUrl = a.FilePath
                        })
                        .ToList(),
                })
                .ToList();
        }

        private async Task CleanupExpiredBulletinPostsAsync(int propertyId)
        {
            var cutoffUtc = DateTime.UtcNow.AddDays(-5);

            var expiredPosts = await _context.BulletinPosts
                .Where(p => p.PropertyId == propertyId && (p.UpdatedAt ?? p.CreatedAt) < cutoffUtc)
                .Include(p => p.Attachments)
                .ToListAsync();

            if (expiredPosts.Count == 0)
            {
                return;
            }

            var attachmentPaths = expiredPosts
                .SelectMany(p => p.Attachments)
                .Select(a => ResolvePhysicalPath(a.FilePath))
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .ToList();

            _context.BulletinPosts.RemoveRange(expiredPosts);
            await _context.SaveChangesAsync();

            foreach (var path in attachmentPaths)
            {
                DeleteFileIfExists(path);
            }
        }

        private async Task<HomeIndexViewModel> BuildHotelLayoutViewModelAsync(Property? property)
        {
            var viewModel = new HomeIndexViewModel
            {
                CurrentProperty = property,
                WidgetHeightDefault = DefaultWidgetHeight,
                WidgetHeightMin = MinWidgetHeight,
                WidgetHeightMax = MaxWidgetHeight,
                WidgetHeightStep = WidgetHeightStep,
                WidgetHeightResetThreshold = WidgetHeightResetThreshold
            };

            if (property != null)
            {
                await PopulateRoomTilesAsync(viewModel, property.Id);
                await PopulateQuickWorkOrderOptionsAsync(viewModel, property.Id);
            }

            return viewModel;
        }

        private async Task PopulateRoomTilesAsync(HomeIndexViewModel viewModel, int propertyId)
        {
            var layoutData = await (
                from layout in _context.RoomLayouts
                where layout.PropertyId == propertyId
                join room in _context.Rooms.Where(r => r.PropertyId == propertyId)
                    on layout.RoomId equals room.Id into roomGroup
                from room in roomGroup.DefaultIfEmpty()
                select new
                {
                    Layout = layout,
                    Room = room
                })
                .AsNoTracking()
                .ToListAsync();

            var floorSequence = layoutData
                .Select(ld => ld.Layout.Floor)
                .Distinct()
                .OrderBy(f => f)
                .ToList();

            var palette = new[]
            {
                "#0d6efd",
                "#6610f2",
                "#198754",
                "#fd7e14",
                "#20c997",
                "#d63384",
                "#6f42c1",
                "#17a2b8",
                "#e83e8c",
                "#7952b3"
            };

            var floorColorMap = new Dictionary<int, string>();
            for (var i = 0; i < floorSequence.Count; i++)
            {
                floorColorMap[floorSequence[i]] = palette[i % palette.Length];
            }

            var activeWorkOrders = await _context.WorkOrderProperties
                .Where(wp => wp.PropertyId == propertyId)
                .Select(wp => wp.WorkOrder)
                .Where(wo => wo.Status == "New" || wo.Status == "In Progress")
                .Select(wo => new
                {
                    wo.Id,
                    wo.Status,
                    wo.Location,
                })
                .AsNoTracking()
                .ToListAsync();

            var openLostFound = await _context.LostFoundEntries
                .Where(lf => lf.PropertyId == propertyId && lf.Status == LostFoundStatus.Logged)
                .Select(lf => new
                {
                    lf.Id,
                    lf.Location,
                })
                .AsNoTracking()
                .ToListAsync();

            foreach (var entry in layoutData)
            {
                var hasRoom = entry.Room != null && entry.Room.Id != 0;
                var baseRoomNumber = hasRoom ? entry.Room!.RoomNumber : null;
                var shapeType = string.IsNullOrWhiteSpace(entry.Layout.ShapeType) ? "rectangle" : entry.Layout.ShapeType!.Trim();
                var shapeData = entry.Layout.ShapeData;
                var isConnector = string.Equals(shapeType, "connector", StringComparison.OrdinalIgnoreCase);
                var displayLabel = !string.IsNullOrWhiteSpace(entry.Layout.Label)
                    ? entry.Layout.Label!.Trim()
                    : (isConnector
                        ? "||"
                        : (!string.IsNullOrWhiteSpace(baseRoomNumber) ? baseRoomNumber! : string.Empty));

                var tile = new RoomLayoutTileViewModel
                {
                    LayoutId = entry.Layout.Id,
                    RoomId = hasRoom ? entry.Room!.Id : 0,
                    RoomNumber = displayLabel,
                    Abbreviation = hasRoom ? entry.Room!.Abbreviation : null,
                    LocationKey = hasRoom ? entry.Room!.RoomNumber : null,
                    RoomType = hasRoom ? entry.Room!.RoomType : (isConnector ? "Connector" : string.Empty),
                    Floor = entry.Layout.Floor,
                    X = entry.Layout.X,
                    Y = entry.Layout.Y,
                    Width = entry.Layout.Width,
                    Height = entry.Layout.Height,
                    ShapeType = shapeType,
                    ShapeData = shapeData,
                    TextRotation = entry.Layout.TextRotation,
                    FloorColor = floorColorMap.TryGetValue(entry.Layout.Floor, out var color)
                        ? color
                        : "#6c757d",
                    CssClass = "room-tile",
                };

                if (isConnector)
                {
                    tile.CssClass = AppendCss(tile.CssClass, "room-tile--connector");
                }
                else if (!hasRoom)
                {
                    tile.CssClass = AppendCss(tile.CssClass, "room-tile--custom");
                }
                else if (!string.IsNullOrWhiteSpace(baseRoomNumber))
                {
                    var matchingOrders = activeWorkOrders
                        .Where(wo => MatchesRoom(baseRoomNumber!, wo.Location))
                        .ToList();

                    if (matchingOrders.Any())
                    {
                        tile.CssClass = AppendCss(tile.CssClass, "room-tile--workorder");
                        tile.Badges.Add(new RoomTileBadgeViewModel
                        {
                            Text = matchingOrders.Count == 1 ? "1 WO" : $"{matchingOrders.Count} WO",
                            Variant = "danger",
                            Url = Url.Action("Index", "WorkOrders"),
                        });
                    }

                    var matchingLostFound = openLostFound
                        .Where(lf => MatchesRoom(baseRoomNumber!, lf.Location))
                        .ToList();

                    if (matchingLostFound.Any())
                    {
                        tile.CssClass = AppendCss(tile.CssClass, "room-tile--lostfound");
                        tile.Badges.Add(new RoomTileBadgeViewModel
                        {
                            Text = matchingLostFound.Count == 1 ? "1 L&F" : $"{matchingLostFound.Count} L&F",
                            Variant = "warning",
                            Url = Url.Action("Index", "LostAndFound"),
                        });
                    }
                }

                viewModel.RoomTiles.Add(tile);
            }

            viewModel.RoomTiles = viewModel.RoomTiles
                .OrderBy(t => t.Floor)
                .ThenBy(t => t.RoomNumber, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private async Task PopulateQuickWorkOrderOptionsAsync(HomeIndexViewModel viewModel, int propertyId)
        {
            viewModel.WorkOrderTypeOptions = await _context.WorkOrderTypes
                .Where(t => !t.PropertyId.HasValue || t.PropertyId.Value == propertyId)
                .OrderBy(t => t.Name)
                .Select(t => new QuickSelectOptionViewModel
                {
                    Id = t.Id,
                    Name = t.Name ?? string.Empty
                })
                .ToListAsync();

            viewModel.DepartmentOptions = await _context.Departments
                .OrderBy(d => d.Name)
                .Select(d => new QuickSelectOptionViewModel
                {
                    Id = d.Id,
                    Name = d.Name ?? string.Empty
                })
                .ToListAsync();

            viewModel.DefaultWorkOrderStatus = _configuration.GetValue<string>("WorkOrders:DefaultStatus") ?? "New";
        }
        private async Task PopulateWorkOrdersAsync(HomeIndexViewModel viewModel, int propertyId)
        {
            var now = DateTime.UtcNow;
            var recentWorkOrders = await _context.WorkOrderProperties
                .Where(wp => wp.PropertyId == propertyId)
                .Select(wp => wp.WorkOrder)
                .Where(wo => wo.Status == "New" || wo.Status == "In Progress" || wo.Status == "Escalated")
                .OrderByDescending(wo => wo.CreatedAt)
                .Select(wo => new
                {
                    WorkOrder = wo,
                    DepartmentId = wo.DepartmentId,
                    DepartmentName = wo.Department != null ? wo.Department.Name : null,
                    DepartmentColor = wo.Department != null && !string.IsNullOrWhiteSpace(wo.Department.Color)
                        ? wo.Department.Color
                        : null,
                    DetailUrl = Url.Action("Edit", "WorkOrders", new { id = wo.Id }) ?? string.Empty
                })
                .AsNoTracking()
                .ToListAsync();

            viewModel.WorkOrders = recentWorkOrders.Select(entry =>
            {
                var sla = WorkOrderSlaHelper.Calculate(entry.WorkOrder.DueDate, now);
                var statusLabel = WorkOrderStatusOptions.GetLabel(entry.WorkOrder.Status);
                return new WorkOrderSummaryViewModel
                {
                    Id = entry.WorkOrder.Id,
                    Status = entry.WorkOrder.Status,
                    StatusLabel = statusLabel ?? entry.WorkOrder.Status ?? string.Empty,
                    TranslatedStatusLabel = statusLabel ?? entry.WorkOrder.Status ?? string.Empty,
                    Issue = entry.WorkOrder.Issue,
                    TranslatedIssue = entry.WorkOrder.Issue ?? string.Empty,
                    Location = entry.WorkOrder.Location,
                    TranslatedLocation = entry.WorkOrder.Location,
                    DepartmentName = entry.DepartmentName,
                    DepartmentId = entry.DepartmentId,
                    TranslatedDepartmentName = entry.DepartmentName,
                    DepartmentColor = entry.DepartmentColor,
                    CreatedAt = entry.WorkOrder.CreatedAt,
                    DueDate = entry.WorkOrder.DueDate,
                    PriorityLabel = sla.PriorityLabel,
                    PriorityClass = sla.PriorityClass,
                    SlaStatus = sla.SlaStatus,
                    SlaStatusClass = sla.SlaStatusClass,
                    SlaSummary = WorkOrderSlaHelper.BuildSummaryText(sla),
                    IsOverdue = sla.IsOverdue,
                    DetailUrl = entry.DetailUrl
                };
            }).ToList();

            var activeLanguage = HttpContext.Items["ActiveLanguage"] as string ?? _translationService.DefaultLanguage;
            var isDefaultLanguage = string.Equals(activeLanguage, _translationService.DefaultLanguage, StringComparison.OrdinalIgnoreCase);
            var cancellationToken = HttpContext.RequestAborted;

            if (!isDefaultLanguage)
            {
                foreach (var workOrder in viewModel.WorkOrders)
                {
                    var entityId = workOrder.Id.ToString(CultureInfo.InvariantCulture);

                    if (!string.IsNullOrWhiteSpace(workOrder.Issue))
                    {
                        workOrder.TranslatedIssue = await _translationService.TranslateDynamicAsync(
                            "WorkOrder",
                            entityId,
                            "Issue",
                            workOrder.Issue!,
                            _translationService.DefaultLanguage,
                            activeLanguage,
                            cancellationToken);
                    }

                    if (!string.IsNullOrWhiteSpace(workOrder.Location))
                    {
                        workOrder.TranslatedLocation = await _translationService.TranslateDynamicAsync(
                            "WorkOrder",
                            entityId,
                            "Location",
                            workOrder.Location!,
                            _translationService.DefaultLanguage,
                            activeLanguage,
                            cancellationToken);
                    }

                    if (!string.IsNullOrWhiteSpace(workOrder.DepartmentName) && workOrder.DepartmentId.HasValue)
                    {
                        workOrder.TranslatedDepartmentName = await _translationService.TranslateDynamicAsync(
                            "Department",
                            workOrder.DepartmentId.Value.ToString(CultureInfo.InvariantCulture),
                            "Name",
                            workOrder.DepartmentName,
                            _translationService.DefaultLanguage,
                            activeLanguage,
                            cancellationToken);
                    }

                    if (!string.IsNullOrWhiteSpace(workOrder.StatusLabel))
                    {
                        workOrder.TranslatedStatusLabel = _translationService.Translate(workOrder.StatusLabel, activeLanguage, workOrder.StatusLabel);
                    }
                    else if (!string.IsNullOrWhiteSpace(workOrder.Status))
                    {
                        workOrder.TranslatedStatusLabel = _translationService.Translate(workOrder.Status, activeLanguage, workOrder.Status);
                    }
                }
            }
            else
            {
                foreach (var workOrder in viewModel.WorkOrders)
                {
                    workOrder.TranslatedStatusLabel = string.IsNullOrWhiteSpace(workOrder.StatusLabel)
                        ? workOrder.Status
                        : workOrder.StatusLabel;
                }
            }

            viewModel.WorkOrderDepartmentSummaries = viewModel.WorkOrders
                .GroupBy(wo => new
                {
                    Id = wo.DepartmentId,
                    Name = string.IsNullOrWhiteSpace(wo.DepartmentName) ? "Unassigned" : wo.DepartmentName,
                    Color = string.IsNullOrWhiteSpace(wo.DepartmentColor) ? null : wo.DepartmentColor
                })
                .Select(group => new WorkOrderDepartmentSummaryViewModel
                {
                    DepartmentId = group.Key.Id,
                    DepartmentName = group.Key.Name ?? "Unassigned",
                    TranslatedDepartmentName = group.Key.Name ?? "Unassigned",
                    DepartmentColor = group.Key.Color,
                    OpenCount = group.Count()
                })
                .OrderByDescending(summary => summary.OpenCount)
                .ThenBy(summary => summary.DepartmentName)
                .ToList();

            if (!isDefaultLanguage)
            {
                foreach (var summary in viewModel.WorkOrderDepartmentSummaries)
                {
                    if (summary.DepartmentId.HasValue && !string.IsNullOrWhiteSpace(summary.DepartmentName))
                    {
                        summary.TranslatedDepartmentName = await _translationService.TranslateDynamicAsync(
                            "Department",
                            summary.DepartmentId.Value.ToString(CultureInfo.InvariantCulture),
                            "Name",
                            summary.DepartmentName,
                            _translationService.DefaultLanguage,
                            activeLanguage,
                            cancellationToken);
                    }
                }
            }
        }

        private async Task PopulateLostFoundAsync(HomeIndexViewModel viewModel, int propertyId)
        {
            var lostFoundEntries = await _context.LostFoundEntries
                .Where(lf => lf.PropertyId == propertyId &&
                    lf.Status != LostFoundStatus.ReturnedToGuest &&
                    lf.Status != LostFoundStatus.DisposedOf)
                .OrderByDescending(lf => lf.CreatedAt)
                .Take(5)
                .Select(lf => new LostFoundSummaryViewModel
                {
                    Id = lf.Id,
                    Title = !string.IsNullOrWhiteSpace(lf.ItemFound) ? lf.ItemFound! : (!string.IsNullOrWhiteSpace(lf.ItemLost) ? lf.ItemLost! : "Lost & Found Entry"),
                    Type = lf.Type,
                    Status = lf.Status,
                    CreatedAt = lf.CreatedAt,
                    DetailUrl = Url.Action("Details", "LostAndFound", new { id = lf.Id }) ?? string.Empty
                })
                .AsNoTracking()
                .ToListAsync();

            viewModel.LostFoundEntries = lostFoundEntries;
        }

        private async Task PopulatePassOnLogsAsync(HomeIndexViewModel viewModel, int propertyId, string currentUserId)
        {
            var logs = await _context.PassOnLogs
                .Where(log => log.Properties.Any(lp => lp.PropertyId == propertyId))
                .Include(log => log.CreatedBy)
                .Include(log => log.Views)
                .OrderByDescending(log => log.CreatedAt)
                .Take(5)
                .AsNoTracking()
                .ToListAsync();

            viewModel.PassOnLogs = logs.Select(log =>
            {
                var creatorName = log.CreatedBy != null ? BuildDisplayName(log.CreatedBy) : "Unknown";
                var preview = string.IsNullOrWhiteSpace(log.Body)
                    ? string.Empty
                    : TruncatePreview(RichTextRenderer.ToPlainText(log.Body ?? string.Empty));

                return new PassOnLogSummaryViewModel
                {
                    Id = log.Id,
                    Title = log.Title,
                    TranslatedTitle = log.Title,
                    Preview = preview,
                    TranslatedPreview = preview,
                    CreatorName = creatorName,
                    CreatorAvatar = UserAvatarHelper.BuildFromUser(log.CreatedBy, creatorName, "sm"),
                    CreatedAt = log.CreatedAt,
                    DetailUrl = Url.Action("Details", "PassOnLogs", new { id = log.Id }) ?? string.Empty,
                    IsRead = log.CreatedById == currentUserId || log.Views.Any(v => v.ViewerId == currentUserId)
                };
            }).ToList();

            var activeLanguage = HttpContext.Items["ActiveLanguage"] as string ?? _translationService.DefaultLanguage;
            var isDefaultLanguage = string.Equals(activeLanguage, _translationService.DefaultLanguage, StringComparison.OrdinalIgnoreCase);
            var cancellationToken = HttpContext.RequestAborted;

            if (!isDefaultLanguage)
            {
                foreach (var summary in viewModel.PassOnLogs)
                {
                    var entityId = summary.Id.ToString(CultureInfo.InvariantCulture);

                    if (!string.IsNullOrWhiteSpace(summary.Title))
                    {
                        var translatedTitle = await _translationService.TranslateDynamicAsync(
                            "PassOnLog",
                            entityId,
                            "Title",
                            summary.Title,
                            _translationService.DefaultLanguage,
                            activeLanguage,
                            cancellationToken);
                        if (!string.IsNullOrWhiteSpace(translatedTitle))
                        {
                            summary.TranslatedTitle = translatedTitle;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(summary.Preview))
                    {
                        var translatedPreview = await _translationService.TranslateDynamicAsync(
                            "PassOnLog",
                            entityId,
                            "Preview",
                            summary.Preview,
                            _translationService.DefaultLanguage,
                            activeLanguage,
                            cancellationToken);
                        if (!string.IsNullOrWhiteSpace(translatedPreview))
                        {
                            summary.TranslatedPreview = translatedPreview;
                        }
                    }
                }
            }
            else
            {
                foreach (var summary in viewModel.PassOnLogs)
                {
                    summary.TranslatedTitle = summary.Title;
                    summary.TranslatedPreview = summary.Preview;
                }
            }
        }

        private async Task PopulateActivityFeedAsync(HomeIndexViewModel viewModel, int propertyId, string currentUserId)
        {
            var feedItems = new List<ActivityFeedItemViewModel>();

            var bulletinPosts = await _context.BulletinPosts
                .Where(post => post.PropertyId == propertyId)
                .Include(post => post.CreatedBy)
                .OrderByDescending(post => post.UpdatedAt ?? post.CreatedAt)
                .Take(5)
                .AsNoTracking()
                .ToListAsync();

            foreach (var post in bulletinPosts)
            {
                var author = post.CreatedBy != null ? BuildDisplayName(post.CreatedBy) : "Team Member";
                var bulletinPreview = TruncatePreview(RichTextRenderer.ToPlainText(post.Content));
                feedItems.Add(new ActivityFeedItemViewModel
                {
                    ItemType = "bulletin",
                    Title = "Bulletin Update",
                    TranslatedTitle = "Bulletin Update",
                    Preview = bulletinPreview,
                    TranslatedPreview = bulletinPreview,
                    CreatorName = author,
                    BadgeText = "Bulletin",
                    BadgeClass = "badge bg-primary",
                    OccurredAt = post.UpdatedAt ?? post.CreatedAt,
                    LinkUrl = $"{Url.Action(nameof(Index), "Home") ?? "/"}#bulletins",
                    Avatar = UserAvatarHelper.BuildFromUser(post.CreatedBy, author, "sm")
                });
            }

            foreach (var log in viewModel.PassOnLogs.Take(5))
            {
                feedItems.Add(new ActivityFeedItemViewModel
                {
                    ItemType = "passon",
                    Title = log.Title,
                    TranslatedTitle = log.TranslatedTitle,
                    Preview = log.Preview,
                    TranslatedPreview = log.TranslatedPreview,
                    CreatorName = log.CreatorName,
                    BadgeText = "Pass On",
                    BadgeClass = "badge bg-success text-white",
                    OccurredAt = log.CreatedAt,
                    LinkUrl = log.DetailUrl,
                    CanReply = true,
                    PassOnLogId = log.Id,
                    ReplyReturnUrl = $"{Url.Action(nameof(Index), "Home") ?? "/"}#opsFeed",
                    Avatar = log.CreatorAvatar
                });
            }

            var activeWorkOrders = await _context.WorkOrders
                .Where(wo => wo.Status == "New" || wo.Status == "In Progress" || wo.Status == "Escalated")
                .Where(wo => wo.Properties.Any(wp => wp.PropertyId == propertyId))
                .OrderByDescending(wo => wo.CreatedAt)
                .Take(5)
                .Select(wo => new
                {
                    wo.Id,
                    wo.Issue,
                    wo.Location,
                    wo.Status,
                    wo.CreatedAt,
                    CreatorId = wo.CreatedById,
                    CreatorFirstName = wo.CreatedBy != null ? wo.CreatedBy.FirstName : null,
                    CreatorLastName = wo.CreatedBy != null ? wo.CreatedBy.LastName : null,
                    CreatorEmail = wo.CreatedBy != null ? wo.CreatedBy.Email : null,
                    CreatorUserName = wo.CreatedBy != null ? wo.CreatedBy.UserName : null
                })
                .AsNoTracking()
                .ToListAsync();

            foreach (var order in activeWorkOrders)
            {
                ApplicationUser? creator = !string.IsNullOrEmpty(order.CreatorId)
                    ? new ApplicationUser
                    {
                        Id = order.CreatorId!,
                        FirstName = order.CreatorFirstName ?? string.Empty,
                        LastName = order.CreatorLastName ?? string.Empty,
                        Email = order.CreatorEmail ?? string.Empty,
                        UserName = order.CreatorUserName ?? string.Empty
                    }
                    : null;

                var creatorName = BuildDisplayName(creator);
                var issueText = order.Issue ?? string.Empty;
                var locationText = order.Location ?? string.Empty;

                feedItems.Add(new ActivityFeedItemViewModel
                {
                    ItemType = "workorder",
                    Title = $"Work Order #{order.Id}",
                    TranslatedTitle = $"Work Order #{order.Id}",
                    Preview = issueText,
                    TranslatedPreview = issueText,
                    CreatorName = creatorName,
                    Meta = locationText,
                    TranslatedMeta = locationText,
                    BadgeText = order.Status,
                    BadgeClass = "badge bg-secondary",
                    OccurredAt = order.CreatedAt,
                    LinkUrl = Url.Action("Edit", "WorkOrders", new { id = order.Id }),
                    Avatar = UserAvatarHelper.BuildFromUser(creator, creatorName, "sm"),
                    WorkOrderId = order.Id
                });
            }

            var mentionAlerts = await _context.UserNotifications
                .Where(n => n.UserId == currentUserId && n.Type == "mention")
                .OrderByDescending(n => n.CreatedAt)
                .Take(5)
                .AsNoTracking()
                .ToListAsync();

            foreach (var alert in mentionAlerts)
            {
                feedItems.Add(new ActivityFeedItemViewModel
                {
                    ItemType = "mention",
                    Title = alert.Title,
                    TranslatedTitle = alert.Title,
                    Preview = alert.Content ?? string.Empty,
                    TranslatedPreview = alert.Content ?? string.Empty,
                    Meta = "Mention",
                    TranslatedMeta = "Mention",
                    BadgeText = "Mention",
                    BadgeClass = "badge bg-info text-dark",
                    OccurredAt = alert.CreatedAt,
                    LinkUrl = alert.LinkUrl
                });
            }

            var activeLanguage = HttpContext.Items["ActiveLanguage"] as string ?? _translationService.DefaultLanguage;
            var isDefaultLanguage = string.Equals(activeLanguage, _translationService.DefaultLanguage, StringComparison.OrdinalIgnoreCase);

            foreach (var item in feedItems)
            {
                item.TranslatedTitle = string.IsNullOrWhiteSpace(item.TranslatedTitle) ? item.Title ?? string.Empty : item.TranslatedTitle;
                item.TranslatedPreview = string.IsNullOrWhiteSpace(item.TranslatedPreview) ? item.Preview ?? string.Empty : item.TranslatedPreview;
                item.TranslatedMeta = string.IsNullOrWhiteSpace(item.TranslatedMeta) ? item.Meta ?? string.Empty : item.TranslatedMeta;
            }

            if (!isDefaultLanguage)
            {
                var cancellationToken = HttpContext.RequestAborted;
                foreach (var item in feedItems)
                {
                    switch (item.ItemType)
                    {
                        case "workorder":
                            if (item.WorkOrderId.HasValue)
                            {
                                var entityId = item.WorkOrderId.Value.ToString(CultureInfo.InvariantCulture);

                                if (!string.IsNullOrWhiteSpace(item.Preview))
                                {
                                    var translatedIssue = await _translationService.TranslateDynamicAsync(
                                        "WorkOrder",
                                        entityId,
                                        "Issue",
                                        item.Preview,
                                        _translationService.DefaultLanguage,
                                        activeLanguage,
                                        cancellationToken);
                                    if (!string.IsNullOrWhiteSpace(translatedIssue))
                                    {
                                        item.TranslatedPreview = translatedIssue;
                                    }
                                }

                                if (!string.IsNullOrWhiteSpace(item.Meta))
                                {
                                    var translatedLocation = await _translationService.TranslateDynamicAsync(
                                        "WorkOrder",
                                        entityId,
                                        "Location",
                                        item.Meta,
                                        _translationService.DefaultLanguage,
                                        activeLanguage,
                                        cancellationToken);
                                    if (!string.IsNullOrWhiteSpace(translatedLocation))
                                    {
                                        item.TranslatedMeta = translatedLocation;
                                    }
                                }

                                var translatedPrefix = _translationService.Translate("Work Order", activeLanguage, "Work Order");
                                item.TranslatedTitle = string.Format(CultureInfo.CurrentCulture, "{0} #{1}", translatedPrefix, item.WorkOrderId.Value);

                                if (!string.IsNullOrWhiteSpace(item.BadgeText))
                                {
                                    item.BadgeText = _translationService.Translate(item.BadgeText, activeLanguage, item.BadgeText);
                                }
                            }
                            break;
                        case "passon":
                            if (!string.IsNullOrWhiteSpace(item.BadgeText))
                            {
                                item.BadgeText = _translationService.Translate(item.BadgeText, activeLanguage, item.BadgeText);
                            }
                            break;
                        case "bulletin":
                            item.TranslatedTitle = _translationService.Translate(item.Title ?? string.Empty, activeLanguage, item.Title ?? string.Empty);
                            if (!string.IsNullOrWhiteSpace(item.BadgeText))
                            {
                                item.BadgeText = _translationService.Translate(item.BadgeText, activeLanguage, item.BadgeText);
                            }
                            break;
                        case "mention":
                            item.TranslatedMeta = _translationService.Translate(item.Meta ?? string.Empty, activeLanguage, item.Meta ?? string.Empty);
                            item.TranslatedTitle = _translationService.Translate(item.Title ?? string.Empty, activeLanguage, item.Title ?? string.Empty);
                            if (!string.IsNullOrWhiteSpace(item.BadgeText))
                            {
                                item.BadgeText = _translationService.Translate(item.BadgeText, activeLanguage, item.BadgeText);
                            }
                            break;
                        default:
                            if (!string.IsNullOrWhiteSpace(item.BadgeText))
                            {
                                item.BadgeText = _translationService.Translate(item.BadgeText, activeLanguage, item.BadgeText);
                            }
                            break;
                    }
                }
            }

            viewModel.ActivityFeed = feedItems
                .OrderByDescending(item => item.OccurredAt)
                .Take(12)
                .ToList();
        }

        private async Task PopulateMyScheduleAsync(HomeIndexViewModel viewModel, int propertyId, string currentUserId)
        {
            var startDate = DateTime.UtcNow.Date;
            var endDate = startDate.AddDays(14);

            var assignments = await _context.ScheduleAssignments
                .AsNoTracking()
                .Where(a =>
                    a.Schedule.PropertyId == propertyId &&
                    a.Schedule.Status == ScheduleStatus.Posted &&
                    a.Employee.IsActive &&
                    a.Employee.ApplicationUserId != null &&
                    a.Employee.ApplicationUserId == currentUserId &&
                    a.ShiftDate >= startDate &&
                    a.ShiftDate <= endDate)
                .OrderBy(a => a.ShiftDate)
                .ThenBy(a => a.ShiftStartTime ?? TimeSpan.Zero)
                .Take(10)
                .Select(a => new MyScheduleShiftViewModel
                {
                    AssignmentId = a.Id,
                    ScheduleId = a.ScheduleId,
                    ScheduleEmployeeId = a.ScheduleEmployeeId,
                    ShiftDate = a.ShiftDate,
                    ShiftName = a.ShiftName,
                    ShiftStartTime = a.ShiftStartTime,
                    ShiftEndTime = a.ShiftEndTime,
                    Notes = a.Notes,
                    ScheduleTitle = string.IsNullOrWhiteSpace(a.Schedule.Title) ? "Weekly Schedule" : a.Schedule.Title,
                    WeekStartDate = a.Schedule.WeekStartDate
                })
                .ToListAsync();

            viewModel.MyScheduleShifts = assignments;
        }

        private async Task PopulatePackageLogAsync(HomeIndexViewModel viewModel, int propertyId)
        {
            var packages = await _context.PackageLogEntries
                .Where(entry => entry.PropertyId == propertyId && !entry.Delivered)
                .OrderByDescending(entry => entry.LoggedAt)
                .Select(entry => new PackageLogSummaryViewModel
                {
                    Id = entry.Id,
                    RecipientName = entry.RecipientName,
                    RoomNumber = entry.RoomNumber,
                    Carrier = entry.Carrier,
                    TrackingNumber = entry.TrackingNumber,
                    StorageLocation = entry.StorageLocation,
                    Delivered = entry.Delivered,
                    DeliveredAt = entry.DeliveredAt,
                    PackageReceivedDate = entry.PackageReceivedDate,
                    LoggedAt = entry.LoggedAt,
                    DetailUrl = Url.Action("Details", "MailLog", new { id = entry.Id }) ?? string.Empty
                })
                .AsNoTracking()
                .ToListAsync();

            viewModel.PackageLogs = packages;
        }

        private async Task PopulateUpcomingEventsAsync(HomeIndexViewModel viewModel, int propertyId)
        {
            var now = DateTime.UtcNow;
            var lookAheadEnd = now.Date.AddMonths(3);

            var events = await _context.CalendarEvents
                .Where(e => e.EventProperties.Any(ep => ep.PropertyId == propertyId))
                .Where(e => e.StartDate <= lookAheadEnd)
                .Include(e => e.Category)
                .Include(e => e.Exceptions)
                .AsNoTracking()
                .ToListAsync();

            static string BuildDateLabel(DateTime start, DateTime end)
            {
                return start.Date == end.Date
                    ? start.ToString("D")
                    : $"{start:MMM d, yyyy} - {end:MMM d, yyyy}";
            }

            static string? BuildTimeLabel(TimeSpan? startTime, TimeSpan? endTime)
            {
                string Format(TimeSpan time) => DateTime.Today.Add(time).ToString("t");

                if (startTime.HasValue && endTime.HasValue)
                {
                    return $"{Format(startTime.Value)} - {Format(endTime.Value)}";
                }

                if (startTime.HasValue)
                {
                    return Format(startTime.Value);
                }

                if (endTime.HasValue)
                {
                    return $"Until {Format(endTime.Value)}";
                }

                return null;
            }

            var displayEvents = events
                .Select(MapCalendarEventForDisplay)
                .ToList();

            var occurrenceSummaries = CalendarRecurrenceHelper
                .ExpandOccurrences(displayEvents, now.Date, lookAheadEnd)
                .Where(e => e.EndDateTime >= now)
                .OrderBy(e => e.StartDateTime)
                .ThenBy(e => e.Title, StringComparer.OrdinalIgnoreCase)
                .Take(5)
                .Select(e => new CalendarEventSummaryViewModel
                {
                    Id = e.Id,
                    Title = e.Title,
                    StartDate = e.StartDate,
                    EndDate = e.EndDate,
                    StartTime = e.StartTime,
                    EndTime = e.EndTime,
                    CategoryName = string.IsNullOrWhiteSpace(e.CategoryName) ? "Event" : e.CategoryName,
                    CategoryColor = string.IsNullOrWhiteSpace(e.CategoryColor) ? "#6c757d" : e.CategoryColor,
                    DetailUrl = Url.Action("Index", "Calendar") ?? string.Empty,
                    DateDisplay = BuildDateLabel(e.StartDate, e.EndDate),
                    TimeDisplay = BuildTimeLabel(e.StartTime, e.EndTime)
                })
                .ToList();

            viewModel.UpcomingEvents = occurrenceSummaries;
        }

        private async Task PopulateCalendarMonthAsync(HomeIndexViewModel viewModel, int propertyId)
        {
            var todayUtc = DateTime.UtcNow;
            var todayDate = todayUtc.Date;
            var monthStart = new DateTime(todayUtc.Year, todayUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var viewStart = DateTime.SpecifyKind(GetStartOfWeek(monthStart, DayOfWeek.Sunday), DateTimeKind.Utc);
            const int totalSlots = 42;
            const int visibleWeekCount = 4;
            var viewEnd = DateTime.SpecifyKind(viewStart.AddDays(totalSlots - 1), DateTimeKind.Utc);

            var events = await _context.CalendarEvents
                .Where(e => e.EventProperties.Any(ep => ep.PropertyId == propertyId))
                .Where(e => e.StartDate <= viewEnd)
                .Include(e => e.Category)
                .Include(e => e.Exceptions)
                .AsNoTracking()
                .ToListAsync();

            var displayEvents = events
                .Select(MapCalendarEventForDisplay)
                .ToList();

            var occurrencesInRange = CalendarRecurrenceHelper
                .ExpandOccurrences(displayEvents, viewStart, viewEnd)
                .ToList();

            var eventLookup = new Dictionary<DateTime, List<CalendarMonthEventBadgeViewModel>>();
            var calendarLink = Url.Action("Index", "Calendar") ?? string.Empty;

            foreach (var occurrence in occurrencesInRange)
            {
                var eventColor = string.IsNullOrWhiteSpace(occurrence.CategoryColor)
                    ? "#0d6efd"
                    : occurrence.CategoryColor;
                var categoryName = string.IsNullOrWhiteSpace(occurrence.CategoryName)
                    ? "Event"
                    : occurrence.CategoryName;

                var occurrenceStart = occurrence.StartDate.Date < viewStart ? viewStart : occurrence.StartDate.Date;
                var occurrenceEnd = occurrence.EndDate.Date > viewEnd ? viewEnd : occurrence.EndDate.Date;

                for (var date = occurrenceStart; date <= occurrenceEnd; date = date.AddDays(1))
                {
                    if (!eventLookup.TryGetValue(date, out var badges))
                    {
                        badges = new List<CalendarMonthEventBadgeViewModel>();
                        eventLookup[date] = badges;
                    }

                    badges.Add(new CalendarMonthEventBadgeViewModel
                    {
                        Title = occurrence.Title,
                        CategoryName = categoryName,
                        Color = eventColor,
                        LinkUrl = calendarLink
                    });
                }
            }

            var weeks = new List<CalendarMonthWeekViewModel>();
            var cursor = viewStart;

            while (cursor <= viewEnd)
            {
                var week = new CalendarMonthWeekViewModel();
                for (var i = 0; i < 7; i++)
                {
                    var currentDate = cursor;
                    eventLookup.TryGetValue(currentDate, out var dayEvents);

                    week.Days.Add(new CalendarMonthDayViewModel
                    {
                        Date = currentDate,
                        IsCurrentMonth = currentDate.Month == monthStart.Month,
                        IsToday = currentDate.Date == todayDate,
                        Events = dayEvents?.OrderBy(e => e.Title).ToList() ?? new List<CalendarMonthEventBadgeViewModel>()
                    });

                    cursor = cursor.AddDays(1);
                }

                weeks.Add(week);
            }

            weeks = TrimCalendarWeeks(weeks, visibleWeekCount, todayDate);

            viewModel.CalendarMonth = new CalendarMonthViewModel
            {
                MonthLabel = monthStart.ToString("MMMM yyyy"),
                MonthStart = monthStart,
                Weeks = weeks
            };
        }

        private static CalendarEventDisplayViewModel MapCalendarEventForDisplay(CalendarEvent calendarEvent)
        {
            var deletedOccurrenceDates = calendarEvent.Exceptions?
                .Where(ex => ex.Type == CalendarEventExceptionType.DeletedOccurrence)
                .Select(ex => ex.OccurrenceDate.Date)
                .ToHashSet() ?? new HashSet<DateTime>();

            var categoryColor = string.IsNullOrWhiteSpace(calendarEvent.Category?.Color)
                ? "#6c757d"
                : calendarEvent.Category!.Color!;

            var categoryName = string.IsNullOrWhiteSpace(calendarEvent.Category?.Name)
                ? "Event"
                : calendarEvent.Category!.Name;

            return new CalendarEventDisplayViewModel
            {
                Id = calendarEvent.Id,
                Title = calendarEvent.Title,
                CategoryName = categoryName,
                CategoryColor = categoryColor,
                CategoryTextColor = "#ffffff",
                StartDate = calendarEvent.StartDate,
                StartTime = calendarEvent.StartTime,
                EndDate = calendarEvent.EndDate,
                EndTime = calendarEvent.EndTime,
                Recurrence = calendarEvent.Recurrence,
                Details = calendarEvent.Details,
                CreatedAtUtc = calendarEvent.CreatedAtUtc,
                PropertyNames = new List<string>(),
                Attachments = new List<CalendarEventAttachmentViewModel>(),
                DeletedOccurrenceDates = deletedOccurrenceDates
            };
        }

        private async Task<bool> UserHasAccessToProperty(int propertyId, ApplicationUser user)
        {
            return await _context.UserPropertyAccesses
                .AnyAsync(upa => upa.PropertyId == propertyId && upa.ApplicationUserId == user.Id);
        }

        private async Task<List<FileSaveResult>> SaveFilesAsync(IEnumerable<IFormFile>? files, string folderName)
        {
            var results = new List<FileSaveResult>();
            if (files == null)
            {
                return results;
            }

            var uploadRoot = Path.Combine(_environment.WebRootPath, "uploads", folderName);
            Directory.CreateDirectory(uploadRoot);

            foreach (var file in files)
            {
                if (file == null || file.Length <= 0)
                {
                    continue;
                }

                var originalFileName = Path.GetFileName(file.FileName);
                var extension = Path.GetExtension(originalFileName);
                var uniqueName = $"{Guid.NewGuid()}{extension}";
                var physicalPath = Path.Combine(uploadRoot, uniqueName);

                using (var stream = System.IO.File.Create(physicalPath))
                {
                    await file.CopyToAsync(stream);
                }

                var relativePath = Path.Combine("/uploads", folderName, uniqueName).Replace("\\", "/");

                results.Add(new FileSaveResult
                {
                    PhysicalPath = physicalPath,
                    RelativePath = relativePath,
                    OriginalFileName = string.IsNullOrWhiteSpace(originalFileName) ? null : originalFileName
                });
            }

            return results;
        }

        private string ResolvePhysicalPath(string? storedPath)
        {
            if (string.IsNullOrWhiteSpace(storedPath))
            {
                return string.Empty;
            }

            var trimmed = storedPath.TrimStart('/', '\\');
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return string.Empty;
            }

            var normalized = trimmed.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            return Path.Combine(_environment.WebRootPath, normalized);
        }
        private static void DeleteFileIfExists(string? physicalPath)
        {
            if (string.IsNullOrWhiteSpace(physicalPath))
            {
                return;
            }

            try
            {
                if (System.IO.File.Exists(physicalPath))
                {
                    System.IO.File.Delete(physicalPath);
                }
            }
            catch
            {
                // swallow cleanup failures
            }
        }

        private static string BuildAttachmentDisplayName(string? originalName, string filePath)
        {
            if (!string.IsNullOrWhiteSpace(originalName))
            {
                return originalName;
            }

            return string.IsNullOrWhiteSpace(filePath)
                ? "Attachment"
                : Path.GetFileName(filePath);
        }

        private static string BuildDisplayName(ApplicationUser? user)
        {
            if (user == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(user.FirstName) || !string.IsNullOrWhiteSpace(user.LastName))
            {
                return string.Join(" ", new[] { user.FirstName, user.LastName }.Where(part => !string.IsNullOrWhiteSpace(part)));
            }

            return string.IsNullOrWhiteSpace(user.Email) ? user.UserName ?? string.Empty : user.Email;
        }

        private static string TruncatePreview(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            var trimmed = input.Trim();
            return trimmed.Length <= 140
                ? trimmed
                : string.Concat(trimmed.AsSpan(0, 140), "...");
        }

        private sealed class FileSaveResult
        {
            public string PhysicalPath { get; init; } = string.Empty;
            public string RelativePath { get; init; } = string.Empty;
            public string? OriginalFileName { get; init; }
        }

        private static string AppendCss(string existing, string cssClass)
        {
            if (string.IsNullOrWhiteSpace(existing))
            {
                return cssClass;
            }

            if (existing.Contains(cssClass, StringComparison.Ordinal))
            {
                return existing;
            }

            return $"{existing} {cssClass}";
        }

        private static bool MatchesRoom(string roomNumber, string? location)
        {
            if (string.IsNullOrWhiteSpace(roomNumber) || string.IsNullOrWhiteSpace(location))
            {
                return false;
            }

            var trimmedLocation = location.Trim();
            var trimmedRoom = roomNumber.Trim();

            if (trimmedLocation.Equals(trimmedRoom, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return trimmedLocation.Contains(trimmedRoom, StringComparison.OrdinalIgnoreCase) ||
                trimmedRoom.Contains(trimmedLocation, StringComparison.OrdinalIgnoreCase);
        }

        private bool IsSafeReturnUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            if (Url.IsLocalUrl(url))
            {
                return true;
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out var destination))
            {
                return false;
            }

            var currentHost = Request.Host;
            if (!currentHost.HasValue)
            {
                return false;
            }

            if (!string.Equals(destination.Host, currentHost.Host, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var currentPort = currentHost.Port;
            if (currentPort.HasValue)
            {
                return destination.Port == currentPort.Value;
            }

            return destination.IsDefaultPort;
        }

        private async Task<List<HomeWidgetLayoutEntry>> BuildWidgetLayoutAsync(string userId, string personaKey, IReadOnlyList<HomeWidgetDefinition> activeDefinitions)
        {
            var layoutRecord = await _context.UserHomeLayouts
                .AsNoTracking()
                .Where(l => l.UserId == userId && l.PersonaKey == personaKey)
                .OrderByDescending(l => l.UpdatedAtUtc)
                .FirstOrDefaultAsync();

            if (layoutRecord == null && !personaKey.Equals(PersonaOptions[0].Key, StringComparison.OrdinalIgnoreCase))
            {
                layoutRecord = await _context.UserHomeLayouts
                    .AsNoTracking()
                    .Where(l => l.UserId == userId && l.PersonaKey == PersonaOptions[0].Key)
                    .OrderByDescending(l => l.UpdatedAtUtc)
                    .FirstOrDefaultAsync();
            }

            List<HomeWidgetLayoutEntry>? storedLayout = null;
            if (layoutRecord != null && !string.IsNullOrWhiteSpace(layoutRecord.LayoutJson))
            {
                try
                {
                    storedLayout = JsonSerializer.Deserialize<List<HomeWidgetLayoutEntry>>(layoutRecord.LayoutJson, LayoutSerializerOptions);
                }
                catch
                {
                    storedLayout = null;
                }
            }
            if (storedLayout == null || storedLayout.Count == 0)
            {
                storedLayout = BuildDefaultWidgetLayout(activeDefinitions);
            }

            var definitions = activeDefinitions.ToDictionary(d => d.Id, d => d, StringComparer.OrdinalIgnoreCase);
            var finalLayout = new List<HomeWidgetLayoutEntry>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (storedLayout != null)
            {
                foreach (var entry in storedLayout)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.WidgetId))
                    {
                        continue;
                    }

                    if (!definitions.TryGetValue(entry.WidgetId, out var definition))
                    {
                        continue;
                    }

                    if (!Enum.IsDefined(typeof(HomeWidgetSize), entry.Size))
                    {
                        entry.Size = definition.DefaultSize;
                    }

                    if (seen.Add(entry.WidgetId))
                    {
                        var sanitizedSpan = entry.CustomSpan.HasValue ? ClampSpan(entry.CustomSpan.Value) : (int?)null;
                        var defaultHeight = definition.DefaultHeight > 0 ? definition.DefaultHeight : DefaultWidgetHeight;
                        var sanitizedHeight = entry.CustomHeight.HasValue ? ClampHeight(entry.CustomHeight.Value) : (int?)null;
                        var spanForColumn = sanitizedSpan ?? GetDefaultSpanForWidget(definition, entry.Size);
                        var sanitizedColumn = entry.ColumnStart.HasValue
                            ? ClampColumnStart(entry.ColumnStart.Value, spanForColumn)
                            : (int?)null;
                        var sanitizedRow = entry.RowStart.HasValue ? ClampRowStart(entry.RowStart.Value) : (int?)null;
                        if (string.Equals(entry.WidgetId, HomeWidgetIds.HotelLayout, StringComparison.OrdinalIgnoreCase))
                        {
                            sanitizedSpan = GetSpanForSize(HomeWidgetSize.Full);
                            sanitizedColumn = 1;
                        }
                        else if (sanitizedSpan.HasValue)
                        {
                            var defaultSpan = GetDefaultSpanForWidget(definition, entry.Size);
                            if (sanitizedSpan.Value == defaultSpan)
                            {
                                sanitizedSpan = null;
                            }
                        }
                        if (sanitizedHeight.HasValue && Math.Abs(sanitizedHeight.Value - defaultHeight) <= WidgetHeightResetThreshold)
                        {
                            sanitizedHeight = null;
                        }

                        finalLayout.Add(new HomeWidgetLayoutEntry
                        {
                            WidgetId = entry.WidgetId,
                            Size = entry.Size,
                            CustomSpan = sanitizedSpan,
                            CustomHeight = sanitizedHeight,
                            ColumnStart = sanitizedColumn,
                            RowStart = sanitizedRow
                        });
                    }
                }
            }

            foreach (var definition in activeDefinitions)
            {
                if (seen.Add(definition.Id))
                {
                    finalLayout.Add(new HomeWidgetLayoutEntry
                    {
                        WidgetId = definition.Id,
                        Size = definition.DefaultSize
                    });
                }
            }

            return finalLayout;
        }

        private static List<HomeWidgetLayoutEntry> BuildDefaultWidgetLayout(IReadOnlyList<HomeWidgetDefinition> activeDefinitions)
        {
            var definitions = activeDefinitions.ToDictionary(d => d.Id, d => d, StringComparer.OrdinalIgnoreCase);
            var layout = new List<HomeWidgetLayoutEntry>();
            foreach (var spec in DefaultWidgetLayout)
            {
                if (!definitions.ContainsKey(spec.WidgetId))
                {
                    continue;
                }

                layout.Add(new HomeWidgetLayoutEntry
                {
                    WidgetId = spec.WidgetId,
                    Size = spec.Size,
                    CustomSpan = spec.CustomSpan,
                    CustomHeight = spec.CustomHeight
                });
            }

            foreach (var definition in activeDefinitions)
            {
                if (layout.Any(entry => entry.WidgetId.Equals(definition.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                layout.Add(new HomeWidgetLayoutEntry
                {
                    WidgetId = definition.Id,
                    Size = definition.DefaultSize
                });
            }

            return layout;
        }

        private static int GetSpanForSize(HomeWidgetSize size) => size switch
        {
            HomeWidgetSize.Full => 12,
            HomeWidgetSize.Half => 6,
            HomeWidgetSize.Third => 4,
            HomeWidgetSize.Quarter => 3,
            _ => 4
        };

        private static int GetDefaultSpanForWidget(HomeWidgetDefinition definition, HomeWidgetSize size)
        {
            if (definition?.DefaultSpanOverride.HasValue == true &&
                definition.DefaultSize == size)
            {
                return ClampSpan(definition.DefaultSpanOverride.Value);
            }

            return GetSpanForSize(size);
        }

        private static int ClampSpan(int span) => Math.Clamp(span, 1, 12);
        private static int ClampColumnStart(int columnStart, int span)
        {
            var normalizedSpan = Math.Clamp(span, 1, 12);
            var maxStart = Math.Max(1, 12 - normalizedSpan + 1);
            return Math.Clamp(columnStart, 1, maxStart);
        }

        private static int ClampRowStart(int rowStart) => Math.Clamp(rowStart, 1, 200);
        private static int ClampHeight(int height) => Math.Clamp(height, MinWidgetHeight, MaxWidgetHeight);

        private sealed record LayoutPersonaOption(string Key, string Name, string Description);

        public class WidgetMarketplaceForm
        {
            public List<string> WidgetIds { get; set; } = new();
        }

        private string ResolvePersonaKey(string? requested)
        {
            var candidate = string.IsNullOrWhiteSpace(requested) ? null : requested.Trim();
            if (!PersonaOptions.Any(p => p.Key.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
            {
                candidate = HttpContext.Session.GetString(PersonaSessionKey);
            }

            if (string.IsNullOrWhiteSpace(candidate) || !PersonaOptions.Any(p => p.Key.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
            {
                candidate = PersonaOptions[0].Key;
            }

            HttpContext.Session.SetString(PersonaSessionKey, candidate);
            return candidate;
        }

        private async Task<IReadOnlyList<HomeWidgetDefinition>> GetActiveWidgetDefinitionsAsync()
        {
            await EnsureMarketplaceSeedAsync();
            var modules = await _context.WidgetMarketplaceModules
                .AsNoTracking()
                .ToListAsync();

            var enabled = modules
                .Where(m => m.IsEnabled)
                .Select(m => m.WidgetId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (enabled.Count == 0)
            {
                enabled.UnionWith(WidgetDefinitions.Select(d => d.Id));
            }

            return WidgetDefinitions
                .Where(def => enabled.Contains(def.Id))
                .ToList();
        }

        private static DateTime GetStartOfWeek(DateTime date, DayOfWeek startOfWeek)
        {
            var diff = (7 + (date.DayOfWeek - startOfWeek)) % 7;
            return date.AddDays(-diff).Date;
        }

        private static List<CalendarMonthWeekViewModel> TrimCalendarWeeks(List<CalendarMonthWeekViewModel> weeks, int visibleWeekCount, DateTime todayDate)
        {
            if (weeks.Count <= visibleWeekCount)
            {
                return weeks;
            }

            var todayWeekIndex = weeks.FindIndex(week => week.Days.Any(day => day.Date.Date == todayDate));
            if (todayWeekIndex < 0)
            {
                return weeks.Take(visibleWeekCount).ToList();
            }

            var minStartIndex = Math.Max(0, todayWeekIndex - (visibleWeekCount - 1));
            var maxStartIndex = weeks.Count - visibleWeekCount;
            var startIndex = Math.Min(minStartIndex, maxStartIndex);

            return weeks.Skip(startIndex).Take(visibleWeekCount).ToList();
        }

        private async Task EnsureMarketplaceSeedAsync()
        {
            var existing = await _context.WidgetMarketplaceModules
                .Select(m => m.WidgetId)
                .ToListAsync();
            var existingSet = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);

            var newModules = WidgetDefinitions
                .Where(def => !existingSet.Contains(def.Id))
                .Select(def => new WidgetMarketplaceModule
                {
                    WidgetId = def.Id,
                    IsEnabled = true
                })
                .ToList();

            if (newModules.Count > 0)
            {
                _context.WidgetMarketplaceModules.AddRange(newModules);
                await _context.SaveChangesAsync();
            }
        }

        private async Task<List<WidgetMarketplaceItemViewModel>> BuildMarketplaceViewModelAsync(IEnumerable<HomeWidgetDefinition> activeDefinitions)
        {
            await EnsureMarketplaceSeedAsync();
            var modules = await _context.WidgetMarketplaceModules
                .AsNoTracking()
                .ToListAsync();

            var definitionLookup = WidgetDefinitions.ToDictionary(d => d.Id, d => d, StringComparer.OrdinalIgnoreCase);
            return modules
                .Select(module =>
                {
                    definitionLookup.TryGetValue(module.WidgetId, out var definition);
                    return new WidgetMarketplaceItemViewModel
                    {
                        WidgetId = module.WidgetId,
                        DisplayName = definition?.DisplayName ?? module.WidgetId,
                        Description = definition?.Description,
                        IsEnabled = module.IsEnabled
                    };
                })
                .OrderBy(m => m.DisplayName)
                .ToList();
        }

        public class UpdateHomeLayoutRequest
        {
            public string? Persona { get; set; }
            public List<UpdateHomeLayoutItem> Widgets { get; set; } = new();
        }

        public class UpdateHomeLayoutItem
        {
            public string WidgetId { get; set; } = string.Empty;
            public string Size { get; set; } = string.Empty;
            public int? CustomSpan { get; set; }
            public int? CustomHeight { get; set; }
            public int? ColumnStart { get; set; }
            public int? RowStart { get; set; }
        }

        public class ManagerAnnouncementForm
        {
            [Required]
            public int PropertyId { get; set; }

            [Required]
            [MaxLength(4000)]
            public string Content { get; set; } = string.Empty;

            public List<IFormFile>? Files { get; set; } = new();

            public List<int> AttachmentsToDelete { get; set; } = new();
        }

        public class BulletinPostForm
        {
            public int? Id { get; set; }

            [Required]
            public int PropertyId { get; set; }

            [Required]
            [MaxLength(2000)]
            public string Content { get; set; } = string.Empty;

            public List<IFormFile>? Files { get; set; } = new();

            public List<int> AttachmentsToDelete { get; set; } = new();
        }
    }
}





