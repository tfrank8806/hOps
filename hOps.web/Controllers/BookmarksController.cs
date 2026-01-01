using System;
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
        public async Task<IActionResult> QuickList(bool includeAll = false)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Unauthorized();
            }

            var currentPropertyId = HttpContext.Session.GetInt32("CurrentPropertyId");
            var roles = await _userManager.GetRolesAsync(currentUser);
            var canManagePropertyBookmarks = roles.Contains("Manager") || roles.Contains("Admin");
            var orderLookup = await _context.BookmarkOrderPreferences
                .Where(p => p.UserId == currentUser.Id)
                .ToDictionaryAsync(p => p.BookmarkId, p => p.SortOrder);

            List<Bookmark> ApplyOrdering(List<Bookmark> source)
            {
                if (!source.Any())
                {
                    return source;
                }

                return source
                    .OrderBy(b => orderLookup.TryGetValue(b.Id, out var sortOrder) ? sortOrder : int.MaxValue)
                    .ThenBy(b => b.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            async Task<List<Bookmark>> LoadBookmarksAsync(IQueryable<Bookmark> query)
            {
                var bookmarks = await query.AsNoTracking().ToListAsync();

                if (!bookmarks.Any())
                {
                    return bookmarks;
                }

                if (includeAll)
                {
                    return ApplyOrdering(bookmarks);
                }

                var flagged = bookmarks.Where(b => b.ShowInQuickMenu).ToList();
                if (flagged.Any())
                {
                    return ApplyOrdering(flagged);
                }

                return ApplyOrdering(bookmarks).Take(5).ToList();
            }

            if (!includeAll)
            {
                var quickList = new List<object>();

                void AppendBookmarks(IEnumerable<Bookmark> bookmarks, string sectionLabel, BookmarkSection sectionType)
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
                            id = bookmark.Id,
                            name = bookmark.Name,
                            url = bookmark.Url,
                            section = sectionLabel,
                            description = bookmark.Description,
                            sectionType = sectionType.ToString()
                        });
                    }
                }

                var userQuery = _context.Bookmarks
                    .Where(b => b.Section == BookmarkSection.User && b.CreatedById == currentUser.Id);

                AppendBookmarks(await LoadBookmarksAsync(userQuery), "Personal", BookmarkSection.User);

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

                    AppendBookmarks(await LoadBookmarksAsync(teamQuery), $"Team - {propertyLabel}", BookmarkSection.Team);

                    if (canManagePropertyBookmarks)
                    {
                        var propertyQuery = _context.Bookmarks
                            .Where(b => b.Section == BookmarkSection.Property && b.PropertyId == currentPropertyId.Value);

                        AppendBookmarks(await LoadBookmarksAsync(propertyQuery), $"Property - {propertyLabel}", BookmarkSection.Property);
                    }
                }

                return Json(quickList);
            }

            var bookmarkLookup = new Dictionary<int, BookmarkDisplayModel>();

            void CollectBookmarks(IEnumerable<Bookmark> bookmarks, string sectionLabel, BookmarkSection sectionType)
            {
                foreach (var bookmark in bookmarks)
                {
                    if (string.IsNullOrWhiteSpace(bookmark.Url))
                    {
                        continue;
                    }

                    if (!bookmarkLookup.ContainsKey(bookmark.Id))
                    {
                        bookmarkLookup[bookmark.Id] = new BookmarkDisplayModel
                        {
                            Id = bookmark.Id,
                            Name = bookmark.Name,
                            Url = bookmark.Url,
                            Description = bookmark.Description,
                            SectionLabel = sectionLabel,
                            SectionType = sectionType.ToString()
                        };
                    }
                }
            }

            List<BookmarkDisplayModel> OrderDisplays(IEnumerable<BookmarkDisplayModel> displays)
            {
                return displays
                    .OrderBy(d => orderLookup.TryGetValue(d.Id, out var sort) ? sort : int.MaxValue)
                    .ThenBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            var includeAllUserQuery = _context.Bookmarks
                .Where(b => b.Section == BookmarkSection.User && b.CreatedById == currentUser.Id);

            CollectBookmarks(await LoadBookmarksAsync(includeAllUserQuery), "Personal", BookmarkSection.User);

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

                CollectBookmarks(await LoadBookmarksAsync(teamQuery), $"Team - {propertyLabel}", BookmarkSection.Team);

                if (canManagePropertyBookmarks)
                {
                    var propertyQuery = _context.Bookmarks
                        .Where(b => b.Section == BookmarkSection.Property && b.PropertyId == currentPropertyId.Value);

                    CollectBookmarks(await LoadBookmarksAsync(propertyQuery), $"Property - {propertyLabel}", BookmarkSection.Property);
                }
            }

            var sectionGroups = await _context.BookmarkSectionGroups
                .Where(g => g.UserId == currentUser.Id)
                .OrderBy(g => g.SortOrder)
                .ThenBy(g => g.Name)
                .Include(g => g.Assignments)
                .ToListAsync();

            var assignmentsLookup = await _context.BookmarkSectionAssignments
                .Where(a => a.UserId == currentUser.Id)
                .ToListAsync();

            var assignedBookmarkIds = new HashSet<int>(assignmentsLookup.Select(a => a.BookmarkId));

            object ConvertDisplay(BookmarkDisplayModel display) => new
            {
                id = display.Id,
                name = display.Name,
                url = display.Url,
                section = display.SectionLabel,
                description = display.Description,
                sectionType = display.SectionType
            };

            var sectionsPayload = sectionGroups.Select(group =>
            {
                var assigned = assignmentsLookup
                    .Where(a => a.SectionGroupId == group.Id)
                    .Select(a => bookmarkLookup.TryGetValue(a.BookmarkId, out var display) ? display : null)
                    .Where(display => display != null)
                    .Cast<BookmarkDisplayModel>()
                    .ToList();

                return new
                {
                    id = group.Id,
                    name = group.Name,
                    bookmarks = OrderDisplays(assigned).Select(ConvertDisplay).ToList()
                };
            }).ToList();

            var ungroupedDisplays = OrderDisplays(bookmarkLookup
                .Where(kvp => !assignedBookmarkIds.Contains(kvp.Key))
                .Select(kvp => kvp.Value))
                .Select(ConvertDisplay)
                .ToList();

            return Json(new
            {
                sections = sectionsPayload,
                ungrouped = ungroupedDisplays
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateOrder([FromBody] BookmarkOrderUpdateRequest request)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Unauthorized();
            }

            var requestedIds = request?.BookmarkIds?
                .Where(id => id > 0)
                .Distinct()
                .ToList() ?? new List<int>();

            if (!requestedIds.Any())
            {
                var existingPreferences = await _context.BookmarkOrderPreferences
                    .Where(p => p.UserId == currentUser.Id)
                    .ToListAsync();

                if (existingPreferences.Any())
                {
                    _context.BookmarkOrderPreferences.RemoveRange(existingPreferences);
                    await _context.SaveChangesAsync();
                }

                return Ok(new { success = true, count = 0 });
            }

            var currentPropertyId = HttpContext.Session.GetInt32("CurrentPropertyId");
            var roles = await _userManager.GetRolesAsync(currentUser);
            var hasPropertyAccess = currentPropertyId.HasValue && (roles.Contains("Admin") || await _context.UserPropertyAccesses
                .AnyAsync(upa => upa.ApplicationUserId == currentUser.Id && upa.PropertyId == currentPropertyId.Value));

            bool CanUseBookmark(Bookmark bookmark)
            {
                return bookmark.Section switch
                {
                    BookmarkSection.User => bookmark.CreatedById == currentUser.Id,
                    BookmarkSection.Team or BookmarkSection.Property => bookmark.PropertyId.HasValue &&
                        currentPropertyId.HasValue &&
                        bookmark.PropertyId.Value == currentPropertyId.Value &&
                        hasPropertyAccess,
                    _ => false
                };
            }

            var bookmarks = await _context.Bookmarks
                .Where(b => requestedIds.Contains(b.Id))
                .ToListAsync();

            var orderLookup = requestedIds
                .Select((id, index) => new { id, index })
                .ToDictionary(x => x.id, x => x.index);

            var orderedBookmarks = bookmarks
                .Where(CanUseBookmark)
                .OrderBy(b => orderLookup[b.Id])
                .ToList();

            if (!orderedBookmarks.Any())
            {
                return Ok(new { success = true, count = 0 });
            }

            var preferenceLookup = await _context.BookmarkOrderPreferences
                .Where(p => p.UserId == currentUser.Id && requestedIds.Contains(p.BookmarkId))
                .ToDictionaryAsync(p => p.BookmarkId);

            for (var i = 0; i < orderedBookmarks.Count; i++)
            {
                var bookmark = orderedBookmarks[i];
                if (preferenceLookup.TryGetValue(bookmark.Id, out var preference))
                {
                    preference.SortOrder = i;
                }
                else
                {
                    _context.BookmarkOrderPreferences.Add(new BookmarkOrderPreference
                    {
                        UserId = currentUser.Id,
                        BookmarkId = bookmark.Id,
                        SortOrder = i
                    });
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true, count = orderedBookmarks.Count });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateSectionOrder([FromBody] BookmarkSectionOrderRequest request)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Unauthorized();
            }

            var requestedIds = request?.SectionIds?
                .Where(id => id > 0)
                .Distinct()
                .ToList() ?? new List<int>();

            var sections = await _context.BookmarkSectionGroups
                .Where(g => g.UserId == currentUser.Id)
                .OrderBy(g => g.SortOrder)
                .ThenBy(g => g.Id)
                .ToListAsync();

            if (!sections.Any())
            {
                return Ok(new { success = true, count = 0 });
            }

            var orderedSections = new List<BookmarkSectionGroup>();
            var included = new HashSet<int>();

            foreach (var sectionId in requestedIds)
            {
                var match = sections.FirstOrDefault(g => g.Id == sectionId);
                if (match != null && included.Add(match.Id))
                {
                    orderedSections.Add(match);
                }
            }

            foreach (var remaining in sections)
            {
                if (included.Contains(remaining.Id))
                {
                    continue;
                }
                orderedSections.Add(remaining);
                included.Add(remaining.Id);
            }

            var updatedCount = 0;
            for (var i = 0; i < orderedSections.Count; i++)
            {
                if (orderedSections[i].SortOrder != i)
                {
                    orderedSections[i].SortOrder = i;
                    updatedCount++;
                }
            }

            if (updatedCount > 0)
            {
                await _context.SaveChangesAsync();
            }

            return Ok(new { success = true, count = orderedSections.Count, updated = updatedCount });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSection([FromBody] BookmarkSectionCreateRequest request)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Unauthorized();
            }

            var name = request?.Name?.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                return BadRequest(new { message = "Section name is required." });
            }

            var maxSort = await _context.BookmarkSectionGroups
                .Where(g => g.UserId == currentUser.Id)
                .Select(g => (int?)g.SortOrder)
                .MaxAsync() ?? 0;

            var group = new BookmarkSectionGroup
            {
                Name = name,
                SortOrder = maxSort + 1,
                UserId = currentUser.Id
            };

            _context.BookmarkSectionGroups.Add(group);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                id = group.Id,
                name = group.Name
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignSection([FromBody] BookmarkAssignSectionRequest request)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Unauthorized();
            }

            if (request == null || request.BookmarkId <= 0)
            {
                return BadRequest(new { message = "Bookmark selection is required." });
            }

            var bookmark = await _context.Bookmarks.FirstOrDefaultAsync(b => b.Id == request.BookmarkId);
            if (bookmark == null)
            {
                return NotFound();
            }

            var currentPropertyId = HttpContext.Session.GetInt32("CurrentPropertyId");
            var roles = await _userManager.GetRolesAsync(currentUser);
            var canOrganize = await CanOrganizeBookmarkAsync(bookmark, currentUser, roles, currentPropertyId);
            if (!canOrganize)
            {
                return Forbid();
            }

            BookmarkSectionGroup? targetGroup = null;
            if (request.SectionId.HasValue)
            {
                targetGroup = await _context.BookmarkSectionGroups
                    .FirstOrDefaultAsync(g => g.Id == request.SectionId.Value && g.UserId == currentUser.Id);
                if (targetGroup == null)
                {
                    return NotFound();
                }
            }

            var existingAssignment = await _context.BookmarkSectionAssignments
                .FirstOrDefaultAsync(a => a.UserId == currentUser.Id && a.BookmarkId == bookmark.Id);

            if (targetGroup == null)
            {
                if (existingAssignment != null)
                {
                    _context.BookmarkSectionAssignments.Remove(existingAssignment);
                    await _context.SaveChangesAsync();
                }

                return Ok(new { success = true });
            }

            if (existingAssignment != null)
            {
                existingAssignment.SectionGroupId = targetGroup.Id;
            }
            else
            {
                _context.BookmarkSectionAssignments.Add(new BookmarkSectionAssignment
                {
                    UserId = currentUser.Id,
                    BookmarkId = bookmark.Id,
                    SectionGroupId = targetGroup.Id
                });
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        private async Task<bool> CanOrganizeBookmarkAsync(Bookmark bookmark, ApplicationUser user, IList<string> roles, int? currentPropertyId)
        {
            if (bookmark.Section == BookmarkSection.User)
            {
                return bookmark.CreatedById == user.Id;
            }

            if (!bookmark.PropertyId.HasValue || !currentPropertyId.HasValue || bookmark.PropertyId.Value != currentPropertyId.Value)
            {
                return false;
            }

            if (roles.Contains("Admin"))
            {
                return true;
            }

            return await _context.UserPropertyAccesses
                .AnyAsync(upa => upa.ApplicationUserId == user.Id && upa.PropertyId == bookmark.PropertyId.Value);
        }

        private sealed class BookmarkDisplayModel
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Url { get; set; } = string.Empty;
            public string? Description { get; set; }
            public string SectionLabel { get; set; } = string.Empty;
            public string SectionType { get; set; } = string.Empty;
        }
    }
}

