using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using hOps.web.Data;
using hOps.web.Models;
using hOps.web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hOps.web.Controllers
{
    [Authorize]
    public class BookmarksController : BaseController
    {
        public BookmarksController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
            : base(context, userManager)
        {
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            var roles = await _userManager.GetRolesAsync(currentUser);
            var currentPropertyId = HttpContext.Session.GetInt32("CurrentPropertyId");

            var vm = await BuildIndexViewModel(currentUser, roles, currentPropertyId, null);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BookmarkFormViewModel form)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            var roles = await _userManager.GetRolesAsync(currentUser);
            var currentPropertyId = HttpContext.Session.GetInt32("CurrentPropertyId");
            var canManagePropertyBookmarks = roles.Contains("Manager") || roles.Contains("Admin");

            if (form.Section == BookmarkSection.Property && !canManagePropertyBookmarks)
            {
                ModelState.AddModelError(nameof(form.Section), "You do not have permission to add property bookmarks.");
            }

            if (form.Section == BookmarkSection.Property || form.Section == BookmarkSection.Team)
            {
                if (!currentPropertyId.HasValue)
                {
                    ModelState.AddModelError(string.Empty, "Select a property before adding bookmarks to this section.");
                }
                else
                {
                    var hasAccess = await _context.UserPropertyAccesses
                        .AnyAsync(upa => upa.ApplicationUserId == currentUser.Id && upa.PropertyId == currentPropertyId.Value);

                    if (!hasAccess)
                    {
                        ModelState.AddModelError(string.Empty, "You do not have access to the selected property.");
                    }
                }
            }

            if (!ModelState.IsValid)
            {
                var invalidVm = await BuildIndexViewModel(currentUser, roles, currentPropertyId, form);
                return View("Index", invalidVm);
            }

            var bookmark = new Bookmark
            {
                Name = form.Name.Trim(),
                Url = form.Url.Trim(),
                Description = string.IsNullOrWhiteSpace(form.Description) ? null : form.Description.Trim(),
                Section = form.Section,
                CreatedById = currentUser.Id,
                PropertyId = form.Section == BookmarkSection.User ? null : currentPropertyId,
                ShowInQuickMenu = false
            };

            _context.Bookmarks.Add(bookmark);
            await _context.SaveChangesAsync();

            TempData["BookmarkSuccess"] = "Bookmark added successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(BookmarkEditViewModel form)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            if (!ModelState.IsValid)
            {
                TempData["BookmarkError"] = "Unable to update bookmark. Please check the form and try again.";
                return RedirectToAction(nameof(Index));
            }

            var roles = await _userManager.GetRolesAsync(currentUser);
            var currentPropertyId = HttpContext.Session.GetInt32("CurrentPropertyId");
            var canManagePropertyBookmarks = roles.Contains("Manager") || roles.Contains("Admin");

            var bookmark = await _context.Bookmarks.FirstOrDefaultAsync(b => b.Id == form.Id);
            if (bookmark == null)
            {
                return NotFound();
            }

            if (!await CanModifyBookmarkAsync(bookmark, currentUser, roles, currentPropertyId))
            {
                return Forbid();
            }

            if (form.Section == BookmarkSection.Property && !canManagePropertyBookmarks)
            {
                TempData["BookmarkError"] = "You do not have permission to save property bookmarks.";
                return RedirectToAction(nameof(Index));
            }

            if (form.Section == BookmarkSection.Property || form.Section == BookmarkSection.Team)
            {
                if (!currentPropertyId.HasValue)
                {
                    TempData["BookmarkError"] = "Select a property before assigning bookmarks to that section.";
                    return RedirectToAction(nameof(Index));
                }

                var hasAccess = await _context.UserPropertyAccesses
                    .AnyAsync(upa => upa.ApplicationUserId == currentUser.Id && upa.PropertyId == currentPropertyId.Value);

                if (!hasAccess)
                {
                    TempData["BookmarkError"] = "You do not have access to the selected property.";
                    return RedirectToAction(nameof(Index));
                }

                bookmark.PropertyId = currentPropertyId.Value;
            }
            else
            {
                bookmark.PropertyId = null;
            }

            bookmark.Name = form.Name.Trim();
            bookmark.Url = form.Url.Trim();
            bookmark.Description = string.IsNullOrWhiteSpace(form.Description) ? null : form.Description.Trim();
            bookmark.Section = form.Section;
            bookmark.ShowInQuickMenu = form.ShowInQuickMenu;

            await _context.SaveChangesAsync();

            TempData["BookmarkSuccess"] = "Bookmark updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            var roles = await _userManager.GetRolesAsync(currentUser);
            var currentPropertyId = HttpContext.Session.GetInt32("CurrentPropertyId");

            var bookmark = await _context.Bookmarks.FirstOrDefaultAsync(b => b.Id == id);
            if (bookmark == null)
            {
                return NotFound();
            }

            if (!await CanModifyBookmarkAsync(bookmark, currentUser, roles, currentPropertyId))
            {
                return Forbid();
            }

            _context.Bookmarks.Remove(bookmark);
            await _context.SaveChangesAsync();

            TempData["BookmarkSuccess"] = "Bookmark deleted successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleQuick(int id, bool showInQuickMenu)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            var roles = await _userManager.GetRolesAsync(currentUser);
            var currentPropertyId = HttpContext.Session.GetInt32("CurrentPropertyId");

            var bookmark = await _context.Bookmarks.FirstOrDefaultAsync(b => b.Id == id);
            if (bookmark == null)
            {
                return NotFound();
            }

            if (!await CanModifyBookmarkAsync(bookmark, currentUser, roles, currentPropertyId))
            {
                return Forbid();
            }

            bookmark.ShowInQuickMenu = showInQuickMenu;
            await _context.SaveChangesAsync();

            TempData["BookmarkSuccess"] = "Quick bookmarks updated.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<BookmarksIndexViewModel> BuildIndexViewModel(
            ApplicationUser currentUser,
            IList<string> roles,
            int? currentPropertyId,
            BookmarkFormViewModel? form)
        {
            form ??= new BookmarkFormViewModel();

            var userBookmarks = await _context.Bookmarks
                .Where(b => b.Section == BookmarkSection.User && b.CreatedById == currentUser.Id)
                .OrderBy(b => b.Name)
                .ToListAsync();

            var teamBookmarksQuery = _context.Bookmarks
                .Include(b => b.CreatedBy)
                .Where(b => b.Section == BookmarkSection.Team);

            var propertyBookmarksQuery = _context.Bookmarks
                .Include(b => b.CreatedBy)
                .Where(b => b.Section == BookmarkSection.Property);

            string? propertyName = null;

            if (currentPropertyId.HasValue)
            {
                teamBookmarksQuery = teamBookmarksQuery.Where(b => b.PropertyId == currentPropertyId.Value);
                propertyBookmarksQuery = propertyBookmarksQuery.Where(b => b.PropertyId == currentPropertyId.Value);

                propertyName = await _context.Properties
                    .Where(p => p.Id == currentPropertyId.Value)
                    .Select(p => p.Name)
                    .FirstOrDefaultAsync();
            }
            else
            {
                teamBookmarksQuery = teamBookmarksQuery.Where(b => false);
                propertyBookmarksQuery = propertyBookmarksQuery.Where(b => false);
            }

            return new BookmarksIndexViewModel
            {
                UserBookmarks = userBookmarks,
                TeamBookmarks = await teamBookmarksQuery.OrderBy(b => b.Name).ToListAsync(),
                PropertyBookmarks = await propertyBookmarksQuery.OrderBy(b => b.Name).ToListAsync(),
                Form = form,
                CanManagePropertyBookmarks = roles.Contains("Manager") || roles.Contains("Admin"),
                HasCurrentProperty = currentPropertyId.HasValue,
                CurrentPropertyName = propertyName,
                CurrentUserId = currentUser.Id
            };
        }

        private async Task<bool> CanModifyBookmarkAsync(
            Bookmark bookmark,
            ApplicationUser currentUser,
            IList<string> roles,
            int? currentPropertyId)
        {
            if (bookmark.Section == BookmarkSection.User)
            {
                return bookmark.CreatedById == currentUser.Id;
            }

            if (!bookmark.PropertyId.HasValue)
            {
                return false;
            }

            if (!currentPropertyId.HasValue || currentPropertyId.Value != bookmark.PropertyId.Value)
            {
                return false;
            }

            var isAdmin = roles.Contains("Admin");
            var isManager = roles.Contains("Manager") || isAdmin;

            var hasAccess = isAdmin || await _context.UserPropertyAccesses
                .AnyAsync(upa => upa.ApplicationUserId == currentUser.Id && upa.PropertyId == bookmark.PropertyId.Value);

            if (!hasAccess)
            {
                return false;
            }

            return bookmark.Section switch
            {
                BookmarkSection.Property => isManager,
                BookmarkSection.Team => bookmark.CreatedById == currentUser.Id || isManager,
                _ => false
            };
        }

        [HttpGet]
        public async Task<IActionResult> QuickList()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Unauthorized();
            }

            var currentPropertyId = HttpContext.Session.GetInt32("CurrentPropertyId");
            var roles = await _userManager.GetRolesAsync(currentUser);
            var canManagePropertyBookmarks = roles.Contains("Manager") || roles.Contains("Admin");
            var quickList = new List<object>();

            async Task<List<Bookmark>> LoadBookmarksAsync(IQueryable<Bookmark> query)
            {
                var flagged = await query.Where(b => b.ShowInQuickMenu).OrderBy(b => b.Name).ToListAsync();
                if (flagged.Any())
                {
                    return flagged;
                }

                return await query.OrderBy(b => b.Name).Take(5).ToListAsync();
            }

            void AppendBookmarks(IEnumerable<Bookmark> bookmarks, string sectionLabel)
            {
                foreach (var bookmark in bookmarks)
                {
                    if (quickList.Count >= 12)
                    {
                        break;
                    }

                    if (string.IsNullOrWhiteSpace(bookmark.Url))
                    {
                        continue;
                    }

                    quickList.Add(new
                    {
                        name = bookmark.Name,
                        url = bookmark.Url,
                        section = sectionLabel
                    });
                }
            }

            var userQuery = _context.Bookmarks
                .Where(b => b.Section == BookmarkSection.User && b.CreatedById == currentUser.Id);

            AppendBookmarks(await LoadBookmarksAsync(userQuery), "Personal");

            if (currentPropertyId.HasValue)
            {
                var propertyName = await _context.Properties
                    .Where(p => p.Id == currentPropertyId.Value)
                    .Select(p => p.Name)
                    .FirstOrDefaultAsync();

                var propertyLabel = string.IsNullOrWhiteSpace(propertyName)
                    ? "Current Property"
                    : propertyName;

                var teamQuery = _context.Bookmarks
                    .Where(b => b.Section == BookmarkSection.Team && b.PropertyId == currentPropertyId.Value);

                AppendBookmarks(await LoadBookmarksAsync(teamQuery), $"Team - {propertyLabel}");

                if (canManagePropertyBookmarks)
                {
                    var propertyQuery = _context.Bookmarks
                        .Where(b => b.Section == BookmarkSection.Property && b.PropertyId == currentPropertyId.Value);

                    AppendBookmarks(await LoadBookmarksAsync(propertyQuery), $"Property - {propertyLabel}");
                }
            }

            return Json(quickList);
        }
    }
}

