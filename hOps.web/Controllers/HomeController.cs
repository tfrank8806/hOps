using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using hOps.web.Data;
using hOps.web.Models;
using hOps.web.ViewModels.Home;
using hOps.web.ViewModels.WorkOrders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hOps.web.Controllers
{
    public class HomeController : BaseController
    {
        public HomeController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
            : base(context, userManager)
        {
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

            var announcement = await _context.ManagerAnnouncements
                .FirstOrDefaultAsync(a => a.PropertyId == form.PropertyId);

            if (announcement == null)
            {
                announcement = new ManagerAnnouncement
                {
                    PropertyId = form.PropertyId,
                    Content = form.Content.Trim(),
                    UpdatedById = user.Id,
                    UpdatedAt = DateTime.UtcNow,
                };

                _context.ManagerAnnouncements.Add(announcement);
            }
            else
            {
                announcement.Content = form.Content.Trim();
                announcement.UpdatedById = user.Id;
                announcement.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
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

            var post = new BulletinPost
            {
                PropertyId = form.PropertyId,
                Content = form.Content.Trim(),
                CreatedAt = DateTime.UtcNow,
                CreatedById = user.Id,
            };

            _context.BulletinPosts.Add(post);
            await _context.SaveChangesAsync();

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

            var post = await _context.BulletinPosts
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

            await _context.SaveChangesAsync();
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

            _context.BulletinPosts.Remove(post);
            await _context.SaveChangesAsync();
            TempData["HomeMessage"] = "Bulletin entry removed.";
            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateAnnouncementAsync(HomeIndexViewModel viewModel, int propertyId)
        {
            var announcement = await _context.ManagerAnnouncements
                .Include(a => a.UpdatedBy)
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
                };
            }
        }

        private async Task PopulateBulletinAsync(HomeIndexViewModel viewModel, int propertyId, ApplicationUser currentUser, HashSet<string> roleSet)
        {
            var posts = await _context.BulletinPosts
                .Where(p => p.PropertyId == propertyId)
                .Include(p => p.CreatedBy)
                .Include(p => p.UpdatedBy)
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
                })
                .ToList();
        }

        private async Task PopulateRoomTilesAsync(HomeIndexViewModel viewModel, int propertyId)
        {
            var layoutData = await _context.RoomLayouts
                .Where(l => l.PropertyId == propertyId)
                .Join(
                    _context.Rooms.Where(r => r.PropertyId == propertyId),
                    layout => layout.RoomId,
                    room => room.Id,
                    (layout, room) => new
                    {
                        Layout = layout,
                        Room = room,
                    })
                .AsNoTracking()
                .ToListAsync();

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
                var tile = new RoomLayoutTileViewModel
                {
                    LayoutId = entry.Layout.Id,
                    RoomId = entry.Room.Id,
                    RoomNumber = entry.Room.RoomNumber,
                    RoomType = entry.Room.RoomType,
                    Floor = entry.Layout.Floor,
                    X = entry.Layout.X,
                    Y = entry.Layout.Y,
                    Width = entry.Layout.Width,
                    Height = entry.Layout.Height,
                    ShapeType = entry.Layout.ShapeType ?? "rectangle",
                    ShapeData = entry.Layout.ShapeData,
                    CssClass = "room-tile",
                };

                var matchingOrders = activeWorkOrders
                    .Where(wo => MatchesRoom(entry.Room.RoomNumber, wo.Location))
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
                    .Where(lf => MatchesRoom(entry.Room.RoomNumber, lf.Location))
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
            var now = DateTime.UtcNow.Date;

            var upcomingEvents = await _context.CalendarEventProperties
                .Where(cep => cep.PropertyId == propertyId)
                .Select(cep => cep.CalendarEvent)
                .Where(e => (e.End ?? e.Start) >= now)
                .OrderBy(e => e.Start)
                .Include(e => e.Category)
                .AsNoTracking()
                .Take(8)
                .ToListAsync();

            viewModel.UpcomingEvents = upcomingEvents
                .Select(e => new CalendarEventSummaryViewModel
                {
                    Id = e.Id,
                    Title = e.Title,
                    Start = e.Start,
                    End = e.End,
                    CategoryName = e.Category?.Name ?? "",
                    CategoryColor = e.Category?.ColorHex,
                    DetailUrl = Url.Action("Index", "Calendar") ?? string.Empty,
                })
                .ToList();
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

        public class ManagerAnnouncementForm
        {
            [Required]
            public int PropertyId { get; set; }

            [Required]
            [MaxLength(4000)]
            public string Content { get; set; } = string.Empty;
        }

        public class BulletinPostForm
        {
            public int? Id { get; set; }

            [Required]
            public int PropertyId { get; set; }

            [Required]
            [MaxLength(2000)]
            public string Content { get; set; } = string.Empty;
        }
    }
}
