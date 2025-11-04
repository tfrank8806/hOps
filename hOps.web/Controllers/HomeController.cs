using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using hOps.web.Data;
using hOps.web.Models;
using hOps.web.Services;
using hOps.web.ViewModels.Home;
using hOps.web.ViewModels.WorkOrders;
using hOps.web.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IO;

namespace hOps.web.Controllers
{
    public class HomeController : BaseController
    {
        private readonly MentionService _mentionService;
        private readonly IWebHostEnvironment _environment;

        public HomeController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            MentionService mentionService,
            IWebHostEnvironment environment)
            : base(context, userManager)
        {
            _mentionService = mentionService;
            _environment = environment;
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
            await PopulatePassOnLogsAsync(viewModel, propertyId);
            await PopulatePackageLogAsync(viewModel, propertyId);
            await PopulateUpcomingEventsAsync(viewModel, propertyId);

            return View(viewModel);
        }

        [HttpGet]
        public IActionResult SwitchProperty(int propertyId)
        {
            HttpContext.Session.SetInt32("CurrentPropertyId", propertyId);
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
            catch
            {
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

            var savedBulletinFiles = await SaveFilesAsync(form.Files, "bulletins");
            var newBulletinAttachments = savedBulletinFiles
                .Select(file => new BulletinPostAttachment
                {
                    FilePath = file.RelativePath,
                    OriginalFileName = file.OriginalFileName
                })
                .ToList();

            foreach (var attachment in newBulletinAttachments)
            {
                post.Attachments.Add(attachment);
            }

            _context.BulletinPosts.Add(post);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch
            {
                foreach (var file in savedBulletinFiles)
                {
                    DeleteFileIfExists(file.PhysicalPath);
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

            var savedBulletinFiles = await SaveFilesAsync(form.Files, "bulletins");
            var newBulletinAttachments = savedBulletinFiles
                .Select(file => new BulletinPostAttachment
                {
                    FilePath = file.RelativePath,
                    OriginalFileName = file.OriginalFileName
                })
                .ToList();
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

            foreach (var attachment in newBulletinAttachments)
            {
                post.Attachments.Add(attachment);
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch
            {
                foreach (var file in savedBulletinFiles)
                {
                    DeleteFileIfExists(file.PhysicalPath);
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

            if (announcement != null)
            {
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
                        .ToList(),
                };
            }
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
                var rawShapeType = entry.Layout.ShapeType;
                var shapeType = string.IsNullOrWhiteSpace(rawShapeType) ? "rectangle" : rawShapeType!.Trim();
                var rawShapeData = entry.Layout.ShapeData;
                var shapeData = string.IsNullOrWhiteSpace(rawShapeData) ? null : rawShapeData!.Trim();
                var isConnector = string.Equals(shapeType, "connector", StringComparison.OrdinalIgnoreCase);
                var connectorSymbol = "||";
                var hasCustomLabel = !string.IsNullOrWhiteSpace(entry.Layout.Label);
                var trimmedLabel = hasCustomLabel ? entry.Layout.Label!.Trim() : string.Empty;
                if (isConnector && (string.Equals(trimmedLabel, "═", StringComparison.Ordinal) || string.Equals(trimmedLabel, "║", StringComparison.Ordinal)))
                {
                    hasCustomLabel = false;
                    trimmedLabel = string.Empty;
                }

                var displayLabel = hasCustomLabel
                    ? trimmedLabel
                    : (isConnector
                        ? connectorSymbol
                        : (!string.IsNullOrWhiteSpace(baseRoomNumber) ? baseRoomNumber! : $"Tile {entry.Layout.Id}"));

                var tile = new RoomLayoutTileViewModel
                {
                    LayoutId = entry.Layout.Id,
                    RoomId = hasRoom ? entry.Room!.Id : 0,
                    RoomNumber = displayLabel,
                    RoomType = hasRoom
                        ? entry.Room!.RoomType
                        : (isConnector ? "Connector" : "Custom Tile"),
                    Floor = entry.Layout.Floor,
                    X = entry.Layout.X,
                    Y = entry.Layout.Y,
                    Width = entry.Layout.Width,
                    Height = entry.Layout.Height,
                    ShapeType = shapeType,
                    ShapeData = shapeData,
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

        private async Task PopulateWorkOrdersAsync(HomeIndexViewModel viewModel, int propertyId)
        {
            var recentWorkOrders = await _context.WorkOrderProperties
                .Where(wp => wp.PropertyId == propertyId)
                .Select(wp => wp.WorkOrder)
                .Where(wo => wo.Status == "New" || wo.Status == "In Progress")
                .OrderByDescending(wo => wo.CreatedAt)
                .Take(8)
                .Select(wo => new WorkOrderSummaryViewModel
                {
                    Id = wo.Id,
                    Status = wo.Status,
                    Issue = wo.Issue,
                    Location = wo.Location,
                    CreatedAt = wo.CreatedAt,
                    DetailUrl = string.Empty,
                })
                .AsNoTracking()
                .ToListAsync();

            foreach (var workOrder in recentWorkOrders)
            {
                workOrder.DetailUrl = Url.Action("Index", "WorkOrders") ?? string.Empty;
            }

            viewModel.WorkOrders = recentWorkOrders;
        }

        private async Task PopulateLostFoundAsync(HomeIndexViewModel viewModel, int propertyId)
        {
            var recentLostFound = await _context.LostFoundEntries
                .Where(lf => lf.PropertyId == propertyId)
                .OrderByDescending(lf => lf.CreatedAt)
                .Take(8)
                .Select(lf => new LostFoundSummaryViewModel
                {
                    Id = lf.Id,
                    Title = lf.Type == LostFoundType.Found ? (lf.ItemFound ?? "Found Item") : (lf.ItemLost ?? "Lost Item"),
                    Type = lf.Type,
                    Status = lf.Status,
                    CreatedAt = lf.CreatedAt,
                    DetailUrl = string.Empty,
                })
                .AsNoTracking()
                .ToListAsync();

            foreach (var entry in recentLostFound)
            {
                entry.DetailUrl = Url.Action("Index", "LostAndFound") ?? string.Empty;
            }

            viewModel.LostFoundEntries = recentLostFound;
        }

        private async Task PopulatePassOnLogsAsync(HomeIndexViewModel viewModel, int propertyId)
        {
            var recentLogs = await _context.PassOnLogs
                .Where(log => log.Properties.Any(lp => lp.PropertyId == propertyId))
                .Include(log => log.CreatedBy)
                .OrderByDescending(log => log.CreatedAt)
                .Take(6)
                .AsNoTracking()
                .ToListAsync();

            viewModel.PassOnLogs = recentLogs
                .Select(log => new PassOnLogSummaryViewModel
                {
                    Id = log.Id,
                    Title = log.Title,
                    Preview = BuildPassOnLogPreview(log.Body),
                    CreatorName = BuildDisplayName(log.CreatedBy),
                    CreatedAt = log.CreatedAt,
                    DetailUrl = Url.Action("Details", "PassOnLogs", new { id = log.Id })
                        ?? Url.Action("Index", "PassOnLogs")
                        ?? string.Empty,
                })
                .ToList();
        }

        private async Task PopulatePackageLogAsync(HomeIndexViewModel viewModel, int propertyId)
        {
            var recentPackages = await _context.PackageLogEntries
                .Where(p => p.PropertyId == propertyId)
                .OrderByDescending(p => p.LoggedAt)
                .Take(8)
                .Select(p => new PackageLogSummaryViewModel
                {
                    Id = p.Id,
                    RecipientName = p.RecipientName,
                    RoomNumber = p.RoomNumber,
                    Carrier = p.Carrier,
                    TrackingNumber = p.TrackingNumber,
                    StorageLocation = p.StorageLocation,
                    Delivered = p.Delivered,
                    DeliveredAt = p.DeliveredAt,
                    LoggedAt = p.LoggedAt,
                    DetailUrl = string.Empty,
                })
                .AsNoTracking()
                .ToListAsync();

            foreach (var entry in recentPackages)
            {
                entry.DetailUrl = Url.Action("Index", "MailLog") ?? string.Empty;
            }

            viewModel.PackageLogs = recentPackages;
        }

        private async Task PopulateUpcomingEventsAsync(HomeIndexViewModel viewModel, int propertyId)
        {
            var now = DateTime.Now;

            var upcomingEventsQuery = _context.CalendarEventProperties
                .Where(cep => cep.PropertyId == propertyId)
                .Select(cep => cep.CalendarEvent)
                .Where(e => e.EndDate >= now.Date)
                .AsNoTracking();

            var upcomingEvents = await _context.CalendarEvents
                .Where(e => upcomingEventsQuery.Contains(e))
                .Include(e => e.Category)
                .AsNoTracking()
                .ToListAsync();

            viewModel.UpcomingEvents = upcomingEvents
                .Select(e =>
                {
                    var start = CombineDateAndTime(e.StartDate, e.StartTime);
                    var displayEnd = e.EndTime.HasValue
                        ? CombineDateAndTime(e.EndDate, e.EndTime)
                        : (DateTime?)null;
                    var endBoundary = CalculateEventEndBoundary(e);

                    return new
                    {
                        Summary = new CalendarEventSummaryViewModel
                        {
                            Id = e.Id,
                            Title = e.Title,
                            Start = start,
                            End = displayEnd,
                            CategoryName = e.Category?.Name ?? string.Empty,
                            CategoryColor = string.IsNullOrWhiteSpace(e.Category?.Color)
                                ? "#6c757d"
                                : e.Category!.Color,
                            DetailUrl = Url.Action("Index", "Calendar") ?? string.Empty,
                        },
                        Start = start,
                        EndBoundary = endBoundary,
                    };
                })
                .Where(x => x.EndBoundary >= now)
                .OrderBy(x => x.Start)
                .Take(8)
                .Select(x => x.Summary)
                .ToList();
        }

        private static DateTime CombineDateAndTime(DateTime date, TimeSpan? time)
        {
            var baseDate = DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified);
            return baseDate.Add(time ?? TimeSpan.Zero);
        }

        private static DateTime CalculateEventEndBoundary(CalendarEvent calendarEvent)
        {
            var endDate = DateTime.SpecifyKind(calendarEvent.EndDate.Date, DateTimeKind.Unspecified);

            if (calendarEvent.EndTime.HasValue)
            {
                return endDate.Add(calendarEvent.EndTime.Value);
            }

            return endDate.AddDays(1).AddTicks(-1);
        }

        private static string BuildPassOnLogPreview(string body)
        {
            var preview = MentionMarkupFormatter.ToDisplayText(body ?? string.Empty)
                .ReplaceLineEndings(" ")
                .Trim();

            if (string.IsNullOrWhiteSpace(preview))
            {
                return string.Empty;
            }

            return preview.Length <= 140
                ? preview
                : $"{preview[..140]}...";
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

                var relativePath = Path.Combine("/uploads", folderName, uniqueName)
                    .Replace("\\", "/");

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

            var normalized = trimmed
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);

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
                // Swallow clean-up failures; files can be removed manually if needed.
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

        private sealed class FileSaveResult
        {
            public string PhysicalPath { get; init; } = string.Empty;
            public string RelativePath { get; init; } = string.Empty;
            public string? OriginalFileName { get; init; }
        }

        public class ManagerAnnouncementForm
        {
            [Required]
            public int PropertyId { get; set; }

            [Required]
            [MaxLength(4000)]
            public string Content { get; set; } = string.Empty;

            public List<IFormFile> Files { get; set; } = new();

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

            public List<IFormFile> Files { get; set; } = new();

            public List<int> AttachmentsToDelete { get; set; } = new();
        }
    }
}
