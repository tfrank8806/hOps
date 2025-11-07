using hOps.web.Data;
using hOps.web.Models;
using hOps.web.ViewModels;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hOps.web.Controllers
{
    [Authorize]
    public class LostAndFoundController : BaseController
    {
        private readonly IWebHostEnvironment _environment;

        public LostAndFoundController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment environment) : base(context, userManager)
        {
            _environment = environment;
        }

        [HttpGet]
        public async Task<IActionResult> Index([FromQuery] LostFoundFilterInput filters)
        {
            var viewModel = await BuildIndexViewModel(filters, null);
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind(Prefix = "Submission")] LostFoundSubmissionViewModel submission)
        {
            var filters = new LostFoundFilterInput();
            var viewModel = await BuildIndexViewModel(filters, submission);

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            var accessiblePropertyIds = viewModel.AccessibleProperties.Select(p => p.Id).ToList();
            var targetPropertyIds = submission.SelectedPropertyIds
                .Where(id => accessiblePropertyIds.Contains(id))
                .Distinct()
                .ToList();

            if (!targetPropertyIds.Any())
            {
                if (accessiblePropertyIds.Count == 1)
                {
                    targetPropertyIds.Add(accessiblePropertyIds.First());
                }
                else
                {
                    ModelState.AddModelError("Submission.SelectedPropertyIds", "Please select at least one property.");
                }
            }

            if (submission.Type == LostFoundType.Found)
            {
                if (!submission.DateFound.HasValue)
                {
                    ModelState.AddModelError("Submission.DateFound", "Date Found is required for found items.");
                }
                if (string.IsNullOrWhiteSpace(submission.ItemFound))
                {
                    ModelState.AddModelError("Submission.ItemFound", "Please provide a description of the found item.");
                }
            }
            else if (submission.Type == LostFoundType.Lost)
            {
                if (!submission.DateReportedLost.HasValue)
                {
                    ModelState.AddModelError("Submission.DateReportedLost", "Date Reported Lost is required for lost items.");
                }
                if (string.IsNullOrWhiteSpace(submission.ItemLost))
                {
                    ModelState.AddModelError("Submission.ItemLost", "Please provide a description of the lost item.");
                }
            }

            if (!ModelState.IsValid)
            {
                viewModel = await BuildIndexViewModel(filters, submission);
                return View("Index", viewModel);
            }

            string? photoPath = null;
            if (submission.Photo != null && submission.Photo.Length > 0)
            {
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "lostfound");
                Directory.CreateDirectory(uploadsFolder);

                var fileExtension = Path.GetExtension(submission.Photo.FileName);
                var fileName = $"{Guid.NewGuid()}{fileExtension}";
                var fullPath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    await submission.Photo.CopyToAsync(stream);
                }

                photoPath = $"/uploads/lostfound/{fileName}";
            }

            var displayName = $"{currentUser.FirstName} {currentUser.LastName}".Trim();
            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = currentUser.Email ?? currentUser.UserName ?? "";
            }

            foreach (var propertyId in targetPropertyIds)
            {
                var entry = new LostFoundEntry
                {
                    PropertyId = propertyId,
                    Type = submission.Type,
                    Status = LostFoundStatus.Logged,
                    DateFound = submission.Type == LostFoundType.Found ? submission.DateFound : null,
                    DateReportedLost = submission.Type == LostFoundType.Lost ? submission.DateReportedLost : null,
                    FoundBy = submission.Type == LostFoundType.Found ? submission.FoundBy?.Trim() : null,
                    GuestName = submission.Type == LostFoundType.Lost ? submission.GuestName?.Trim() : null,
                    GuestPhone = submission.Type == LostFoundType.Lost ? submission.GuestPhone?.Trim() : null,
                    GuestAddress = submission.Type == LostFoundType.Lost ? submission.GuestAddress?.Trim() : null,
                    Location = submission.Location?.Trim(),
                    ItemFound = submission.Type == LostFoundType.Found ? submission.ItemFound?.Trim() : null,
                    ItemLost = submission.Type == LostFoundType.Lost ? submission.ItemLost?.Trim() : null,
                    Notes = submission.Notes?.Trim(),
                    Stored = submission.Type == LostFoundType.Found ? submission.Stored?.Trim() : null,
                    PhotoPath = photoPath,
                    CreatedAt = DateTime.UtcNow,
                    CreatedByUserId = currentUser.Id,
                    CreatedByDisplayName = displayName
                };

                _context.LostFoundEntries.Add(entry);
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Lost & Found entry logged successfully.";

            var redirectPropertyIds = targetPropertyIds.Any()
                ? targetPropertyIds
                : filters.PropertyIds;

            return RedirectToAction(nameof(Index), new { propertyIds = redirectPropertyIds });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, LostFoundStatus status)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            var accessiblePropertyIds = await _context.UserPropertyAccesses
                .Where(upa => upa.ApplicationUserId == currentUser.Id)
                .Select(upa => upa.PropertyId)
                .ToListAsync();

            var entry = await _context.LostFoundEntries
                .FirstOrDefaultAsync(e => e.Id == id);

            if (entry == null)
            {
                return NotFound();
            }

            if (!accessiblePropertyIds.Contains(entry.PropertyId))
            {
                return Forbid();
            }

            entry.Status = status;
            _context.LostFoundEntries.Update(entry);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Entry status updated.";
            return RedirectToAction(nameof(Index), new { propertyIds = new[] { entry.PropertyId } });
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            var accessiblePropertyIds = await _context.UserPropertyAccesses
                .Where(upa => upa.ApplicationUserId == currentUser.Id)
                .Select(upa => upa.PropertyId)
                .ToListAsync();

            var entry = await _context.LostFoundEntries
                .FirstOrDefaultAsync(e => e.Id == id);

            if (entry == null)
            {
                return NotFound();
            }

            if (!accessiblePropertyIds.Contains(entry.PropertyId))
            {
                return Forbid();
            }

            if (!string.IsNullOrWhiteSpace(entry.PhotoPath))
            {
                try
                {
                    var trimmed = entry.PhotoPath.TrimStart('/', '\\');
                    var normalized = trimmed.Replace('/', Path.DirectorySeparatorChar)
                        .Replace('\\', Path.DirectorySeparatorChar);
                    var fullPath = Path.Combine(_environment.WebRootPath, normalized);
                    if (System.IO.File.Exists(fullPath))
                    {
                        System.IO.File.Delete(fullPath);
                    }
                }
                catch
                {
                    // ignored - failure to delete photo shouldn't block entry removal
                }
            }

            _context.LostFoundEntries.Remove(entry);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Lost & Found entry deleted.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<LostAndFoundIndexViewModel> BuildIndexViewModel(LostFoundFilterInput filters, LostFoundSubmissionViewModel? submission)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return new LostAndFoundIndexViewModel();
            }

            filters ??= new LostFoundFilterInput();
            filters.Normalize();

            var accessibleProperties = await _context.UserPropertyAccesses
                .Where(upa => upa.ApplicationUserId == currentUser.Id)
                .Select(upa => upa.Property)
                .OrderBy(p => p.Name)
                .ToListAsync();

            var accessiblePropertyIds = accessibleProperties.Select(p => p.Id).ToList();

            if (!accessiblePropertyIds.Any())
            {
                return new LostAndFoundIndexViewModel();
            }

            var normalizedPropertyFilters = filters.PropertyIds
                .Where(id => accessiblePropertyIds.Contains(id))
                .Distinct()
                .ToList();

            if (!normalizedPropertyFilters.Any())
            {
                var sessionPropertyId = HttpContext.Session.GetInt32("CurrentPropertyId");
                if (sessionPropertyId.HasValue && accessiblePropertyIds.Contains(sessionPropertyId.Value))
                {
                    normalizedPropertyFilters.Add(sessionPropertyId.Value);
                }
            }

            filters.PropertyIds = normalizedPropertyFilters;

            var entriesQuery = _context.LostFoundEntries
                .Include(e => e.Property)
                .Include(e => e.CreatedByUser)
                .Where(e => accessiblePropertyIds.Contains(e.PropertyId));

            if (normalizedPropertyFilters.Any())
            {
                entriesQuery = entriesQuery.Where(e => normalizedPropertyFilters.Contains(e.PropertyId));
            }

            var entries = await entriesQuery.ToListAsync();

            IEnumerable<LostFoundEntry> filteredEntries = entries;

            if (filters.DateFrom.HasValue)
            {
                var from = filters.DateFrom.Value.Date;
                filteredEntries = filteredEntries.Where(e =>
                    (e.Type == LostFoundType.Found && e.DateFound?.Date >= from) ||
                    (e.Type == LostFoundType.Lost && e.DateReportedLost?.Date >= from) ||
                    ((!e.DateFound.HasValue && !e.DateReportedLost.HasValue) && e.CreatedAt.Date >= from));
            }

            if (filters.DateTo.HasValue)
            {
                var to = filters.DateTo.Value.Date;
                filteredEntries = filteredEntries.Where(e =>
                    (e.Type == LostFoundType.Found && e.DateFound?.Date <= to) ||
                    (e.Type == LostFoundType.Lost && e.DateReportedLost?.Date <= to) ||
                    ((!e.DateFound.HasValue && !e.DateReportedLost.HasValue) && e.CreatedAt.Date <= to));
            }

            if (!string.IsNullOrEmpty(filters.RoomNumber))
            {
                filteredEntries = filteredEntries.Where(e => !string.IsNullOrWhiteSpace(e.Location) &&
                    e.Location.Contains(filters.RoomNumber!, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(filters.GuestName))
            {
                filteredEntries = filteredEntries.Where(e => !string.IsNullOrWhiteSpace(e.GuestName) &&
                    e.GuestName.Contains(filters.GuestName!, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(filters.FoundBy))
            {
                filteredEntries = filteredEntries.Where(e => !string.IsNullOrWhiteSpace(e.FoundBy) &&
                    e.FoundBy.Contains(filters.FoundBy!, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(filters.Creator))
            {
                filteredEntries = filteredEntries.Where(e =>
                    (!string.IsNullOrWhiteSpace(e.CreatedByDisplayName) && e.CreatedByDisplayName.Contains(filters.Creator!, StringComparison.OrdinalIgnoreCase)) ||
                    (e.CreatedByUser != null && (
                        (!string.IsNullOrWhiteSpace(e.CreatedByUser.FirstName) && e.CreatedByUser.FirstName.Contains(filters.Creator!, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrWhiteSpace(e.CreatedByUser.LastName) && e.CreatedByUser.LastName.Contains(filters.Creator!, StringComparison.OrdinalIgnoreCase))
                    )));
            }

            if (!string.IsNullOrEmpty(filters.Keyword))
            {
                filteredEntries = filteredEntries.Where(e =>
                    (e.Location?.Contains(filters.Keyword!, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (e.Notes?.Contains(filters.Keyword!, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (e.ItemFound?.Contains(filters.Keyword!, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (e.ItemLost?.Contains(filters.Keyword!, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (e.Stored?.Contains(filters.Keyword!, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            if (filters.Statuses.Any())
            {
                var statusSet = new HashSet<LostFoundStatus>(filters.Statuses);
                filteredEntries = filteredEntries.Where(e => statusSet.Contains(e.Status));
            }

            filteredEntries = filters.SortOrder == "oldest"
                ? filteredEntries.OrderBy(e => e.CreatedAt)
                : filteredEntries.OrderByDescending(e => e.CreatedAt);

            var foundEntries = filteredEntries.Where(e => e.Type == LostFoundType.Found).ToList();
            var lostEntries = filteredEntries.Where(e => e.Type == LostFoundType.Lost).ToList();

            var rooms = await _context.Rooms
                .Where(r => accessiblePropertyIds.Contains(r.PropertyId))
                .Select(r => r.RoomNumber)
                .ToListAsync();

            var locationOptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var room in rooms)
            {
                if (!string.IsNullOrWhiteSpace(room))
                {
                    locationOptions.Add(room);
                }
            }

            foreach (var entry in entries)
            {
                if (!string.IsNullOrWhiteSpace(entry.Location))
                {
                    locationOptions.Add(entry.Location);
                }
            }

            var foundByOptions = await _context.UserPropertyAccesses
                .Where(upa => accessiblePropertyIds.Contains(upa.PropertyId))
                .Include(upa => upa.ApplicationUser)
                .Select(upa => (upa.ApplicationUser.FirstName + " " + upa.ApplicationUser.LastName).Trim())
                .Distinct()
                .ToListAsync();

            foreach (var entry in entries)
            {
                if (!string.IsNullOrWhiteSpace(entry.FoundBy))
                {
                    foundByOptions.Add(entry.FoundBy);
                }
            }

            var creatorOptions = entries
                .Select(e => !string.IsNullOrWhiteSpace(e.CreatedByDisplayName)
                    ? e.CreatedByDisplayName
                    : e.CreatedByUser != null
                        ? $"{e.CreatedByUser.FirstName} {e.CreatedByUser.LastName}".Trim()
                        : string.Empty)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n)
                .ToList();

            submission ??= new LostFoundSubmissionViewModel();

            submission.SelectedPropertyIds = submission.SelectedPropertyIds
                .Where(id => accessiblePropertyIds.Contains(id))
                .Distinct()
                .ToList();

            if (!submission.DateFound.HasValue)
            {
                submission.DateFound = DateTime.Today;
            }
            if (!submission.DateReportedLost.HasValue)
            {
                submission.DateReportedLost = DateTime.Today;
            }

            if (!submission.SelectedPropertyIds.Any())
            {
                if (normalizedPropertyFilters.Any())
                {
                    submission.SelectedPropertyIds = normalizedPropertyFilters.ToList();
                }
                else if (accessiblePropertyIds.Any())
                {
                    submission.SelectedPropertyIds.Add(accessiblePropertyIds.First());
                }
            }

            return new LostAndFoundIndexViewModel
            {
                Filters = filters,
                Submission = submission,
                FoundEntries = foundEntries,
                LostEntries = lostEntries,
                AccessibleProperties = accessibleProperties,
                LocationOptions = locationOptions.OrderBy(x => x).ToList(),
                FoundByOptions = foundByOptions.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList(),
                CreatorOptions = creatorOptions
            };
        }
    }
}
