using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using hOps.web.Data;
using hOps.web.Models;
using hOps.web.Services;
using hOps.web.Utilities;
using hOps.web.ViewModels.Home;
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
    public class HomeController : BaseController
    {
        private readonly MentionService _mentionService;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<HomeController> _logger;
        private readonly IConfiguration _configuration;

        private static readonly IReadOnlyList<HomeWidgetDefinition> WidgetDefinitions = new[]
        {
            new HomeWidgetDefinition { Id = HomeWidgetIds.Announcements, DefaultSize = HomeWidgetSize.Third },
            new HomeWidgetDefinition { Id = HomeWidgetIds.Bulletins, DefaultSize = HomeWidgetSize.Third },
            new HomeWidgetDefinition { Id = HomeWidgetIds.PassOnLogs, DefaultSize = HomeWidgetSize.Third },
            new HomeWidgetDefinition { Id = HomeWidgetIds.PackageLog, DefaultSize = HomeWidgetSize.Quarter },
            new HomeWidgetDefinition { Id = HomeWidgetIds.UpcomingEvents, DefaultSize = HomeWidgetSize.Quarter },
            new HomeWidgetDefinition { Id = HomeWidgetIds.WorkOrders, DefaultSize = HomeWidgetSize.Quarter },
            new HomeWidgetDefinition { Id = HomeWidgetIds.LostFound, DefaultSize = HomeWidgetSize.Quarter },
            new HomeWidgetDefinition { Id = HomeWidgetIds.HotelLayout, DefaultSize = HomeWidgetSize.Full }
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
            ILogger<HomeController> logger)
            : base(context, userManager)
        {
            _mentionService = mentionService;
            _environment = environment;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<IActionResult> Index()
        {
            ViewData["Title"] = "Home";

            var viewModel = new HomeIndexViewModel();
            var currentProperty = ViewBag.CurrentProperty as Property;
            viewModel.CurrentProperty = currentProperty;

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return View(viewModel);
            }

            viewModel.WidgetLayout = await BuildWidgetLayoutAsync(user.Id);
            viewModel.WidgetSizeClasses = WidgetSizeClasses.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            var userRoles = await _userManager.GetRolesAsync(user);
            var roleSet = new HashSet<string>(userRoles, StringComparer.OrdinalIgnoreCase);
            viewModel.CanManageAnnouncements = roleSet.Contains("Admin") || roleSet.Contains("Manager");

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
            await PopulateQuickWorkOrderOptionsAsync(viewModel, propertyId);

            return View(viewModel);
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
        public async Task<IActionResult> SaveWidgetLayout([FromBody] UpdateHomeLayoutRequest request)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Unauthorized();
            }

            if (request?.Widgets == null || request.Widgets.Count == 0)
            {
                return BadRequest(new { message = "No widgets were provided." });
            }

            var definitions = WidgetDefinitions.ToDictionary(d => d.Id, d => d, StringComparer.OrdinalIgnoreCase);
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

                if (!seen.Add(widget.WidgetId))
                {
                    continue;
                }

                normalized.Add(new HomeWidgetLayoutEntry
                {
                    WidgetId = widget.WidgetId,
                    Size = parsedSize
                });
            }

            foreach (var definition in WidgetDefinitions)
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
            var layout = await _context.UserHomeLayouts.FirstOrDefaultAsync(l => l.UserId == currentUser.Id);
            if (layout == null)
            {
                layout = new UserHomeLayout
                {
                    UserId = currentUser.Id,
                    LayoutJson = serialized,
                    UpdatedAtUtc = DateTime.UtcNow
                };
                _context.UserHomeLayouts.Add(layout);
            }
            else
            {
                layout.LayoutJson = serialized;
                layout.UpdatedAtUtc = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true });
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
            var recentWorkOrders = await _context.WorkOrderProperties
                .Where(wp => wp.PropertyId == propertyId)
                .Select(wp => wp.WorkOrder)
                .Where(wo => wo.Status == "New" || wo.Status == "In Progress")
                .OrderByDescending(wo => wo.CreatedAt)
                .Take(5)
                .Select(wo => new WorkOrderSummaryViewModel
                {
                    Id = wo.Id,
                    Status = wo.Status,
                    Issue = wo.Issue,
                    Location = wo.Location,
                    DepartmentName = wo.Department != null ? wo.Department.Name : null,
                    DepartmentColor = wo.Department != null && !string.IsNullOrWhiteSpace(wo.Department.Color)
                        ? wo.Department.Color
                        : null,
                    CreatedAt = wo.CreatedAt,
                    DetailUrl = Url.Action("Edit", "WorkOrders", new { id = wo.Id }) ?? string.Empty
                })
                .AsNoTracking()
                .ToListAsync();

            viewModel.WorkOrders = recentWorkOrders;
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
                    Preview = preview,
                    CreatorName = creatorName,
                    CreatorAvatar = UserAvatarHelper.BuildFromUser(log.CreatedBy, creatorName, "sm"),
                    CreatedAt = log.CreatedAt,
                    DetailUrl = Url.Action("Details", "PassOnLogs", new { id = log.Id }) ?? string.Empty,
                    IsRead = log.CreatedById == currentUserId || log.Views.Any(v => v.ViewerId == currentUserId)
                };
            }).ToList();
        }

        private async Task PopulatePackageLogAsync(HomeIndexViewModel viewModel, int propertyId)
        {
            var packages = await _context.PackageLogEntries
                .Where(entry => entry.PropertyId == propertyId && !entry.Delivered)
                .OrderByDescending(entry => entry.LoggedAt)
                .Take(5)
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
            var events = await _context.CalendarEvents
                .Where(e => e.EventProperties.Any(ep => ep.PropertyId == propertyId))
                .Include(e => e.Category)
                .Where(e => e.EndDate >= now.Date)
                .OrderBy(e => e.StartDate)
                .Take(5)
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

            var summaries = events
                .Select(e =>
                {
                    var dateLabel = BuildDateLabel(e.StartDate, e.EndDate);
                    var timeLabel = BuildTimeLabel(e.StartTime, e.EndTime);

                    return new CalendarEventSummaryViewModel
                    {
                        Id = e.Id,
                        Title = e.Title,
                        StartDate = e.StartDate,
                        EndDate = e.EndDate,
                        StartTime = e.StartTime,
                        EndTime = e.EndTime,
                        CategoryName = e.Category?.Name ?? "Event",
                        CategoryColor = string.IsNullOrWhiteSpace(e.Category?.Color) ? "#6c757d" : e.Category!.Color,
                        DetailUrl = Url.Action("Index", "Calendar") ?? string.Empty,
                        DateDisplay = dateLabel,
                        TimeDisplay = timeLabel
                    };
                })
                .ToList();

            viewModel.UpcomingEvents = summaries;
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

        private async Task<List<HomeWidgetLayoutEntry>> BuildWidgetLayoutAsync(string userId)
        {
            var layoutRecord = await _context.UserHomeLayouts
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.UserId == userId);

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

            var definitions = WidgetDefinitions.ToDictionary(d => d.Id, d => d, StringComparer.OrdinalIgnoreCase);
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
                        finalLayout.Add(new HomeWidgetLayoutEntry
                        {
                            WidgetId = entry.WidgetId,
                            Size = entry.Size
                        });
                    }
                }
            }

            foreach (var definition in WidgetDefinitions)
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

        public class UpdateHomeLayoutRequest
        {
            public List<UpdateHomeLayoutItem> Widgets { get; set; } = new();
        }

        public class UpdateHomeLayoutItem
        {
            public string WidgetId { get; set; } = string.Empty;
            public string Size { get; set; } = string.Empty;
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





