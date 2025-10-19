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

            if (!ModelState.IsValid)
            {
                var invalidVm = await BuildIndexViewModel(currentUser, roles, currentPropertyId, form);
                return View("Index", invalidVm);
            }

            if (form.Section == BookmarkSection.Property && !canManagePropertyBookmarks)
            {
                ModelState.AddModelError(nameof(form.Section), "You do not have permission to add property bookmarks.");
            }

            if ((form.Section == BookmarkSection.Property || form.Section == BookmarkSection.Team))
            {
                if (!currentPropertyId.HasValue)
                {
                    ModelState.AddModelError(string.Empty, "Please select a property before adding bookmarks to this section.");
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
                var vm = await BuildIndexViewModel(currentUser, roles, currentPropertyId, form);
                return View("Index", vm);
            }

            var bookmark = new Bookmark
            {
                Name = form.Name.Trim(),
                Url = form.Url.Trim(),
                Description = string.IsNullOrWhiteSpace(form.Description) ? null : form.Description.Trim(),
                Section = form.Section,
                CreatedById = currentUser.Id,
                PropertyId = form.Section == BookmarkSection.User ? null : currentPropertyId
            };

            _context.Bookmarks.Add(bookmark);
            await _context.SaveChangesAsync();

            TempData["BookmarkSuccess"] = "Bookmark added successfully.";
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

            var vm = new BookmarksIndexViewModel
            {
                UserBookmarks = userBookmarks,
                TeamBookmarks = await teamBookmarksQuery
                    .OrderBy(b => b.Name)
                    .ToListAsync(),
                PropertyBookmarks = await propertyBookmarksQuery
                    .OrderBy(b => b.Name)
                    .ToListAsync(),
                Form = form,
                CanManagePropertyBookmarks = roles.Contains("Manager") || roles.Contains("Admin"),
                HasCurrentProperty = currentPropertyId.HasValue,
                CurrentPropertyName = propertyName
            };

            return vm;
        }
    }
}
