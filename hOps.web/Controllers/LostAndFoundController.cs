using hOps.web.Data;
using hOps.web.Models;
using hOps.web.ViewModels;
using hOps.web.ViewModels.WorkOrders;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using hOps.web.Services.Localization;

namespace hOps.web.Controllers
{
    [Authorize]
    public class LostAndFoundController : BaseController
    {
        private readonly IWebHostEnvironment _environment;
        private readonly ITranslationService _translationService;

        public LostAndFoundController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment environment,
            ITranslationService translationService) : base(context, userManager)
        {
            _environment = environment;
            _translationService = translationService;
        }

        private string GetActiveLanguage()
            => HttpContext?.Items?["ActiveLanguage"] as string ?? _translationService.DefaultLanguage;

        private string Translate(string key, string? fallback = null)
            => _translationService.Translate(key, GetActiveLanguage(), fallback ?? key);


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
            submission ??= new LostFoundSubmissionViewModel();

            var filters = new LostFoundFilterInput();
            var viewModel = await BuildIndexViewModel(filters, submission);

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            var accessiblePropertyIds = viewModel.AccessibleProperties.Select(p => p.Id).ToList();
            submission.SelectedPropertyIds ??= new List<int>();

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
                    ModelState.AddModelError("Submission.SelectedPropertyIds", Translate("Please select at least one property."));
                }
            }

            if (submission.Type == LostFoundType.Found)
            {
                if (!submission.DateFound.HasValue)
                {
                    ModelState.AddModelError("Submission.DateFound", Translate("Date Found is required for found items."));
                }
                if (string.IsNullOrWhiteSpace(submission.ItemFound))
                {
                    ModelState.AddModelError("Submission.ItemFound", Translate("Please provide a description of the found item."));
                }
            }
            else if (submission.Type == LostFoundType.Lost)
            {
                if (!submission.DateReportedLost.HasValue)
                {
                    ModelState.AddModelError("Submission.DateReportedLost", Translate("Date Reported Lost is required for lost items."));
                }
                if (string.IsNullOrWhiteSpace(submission.ItemLost))
                {
                    ModelState.AddModelError("Submission.ItemLost", Translate("Please provide a description of the lost item."));
                }
            }

            if (!ModelState.IsValid)
            {
                viewModel = await BuildIndexViewModel(filters, submission);
                return View("Index", viewModel);
            }

            var housekeepingDepartments = new Dictionary<int, Department>();
            Department? fallbackHousekeepingDepartment = null;

            if (targetPropertyIds.Any())
            {
                var candidateDepartments = await _context.Departments
                    .Where(d => !d.PropertyId.HasValue || targetPropertyIds.Contains(d.PropertyId.Value))
                    .ToListAsync();

                foreach (var department in candidateDepartments)
                {
                    if (string.IsNullOrWhiteSpace(department.Name) ||
                        !department.Name.Equals("Housekeeping", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (department.PropertyId.HasValue)
                    {
                        var propertyId = department.PropertyId.Value;
                        if (!housekeepingDepartments.ContainsKey(propertyId))
                        {
                            housekeepingDepartments[propertyId] = department;
                        }
                    }
                    else if (fallbackHousekeepingDepartment == null)
                    {
                        fallbackHousekeepingDepartment = department;
                    }
                }
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
                    DateFound = submission.Type == LostFoundType.Found ? NormalizeDateToUtc(submission.DateFound) : null,
                    DateReportedLost = submission.Type == LostFoundType.Lost ? NormalizeDateToUtc(submission.DateReportedLost) : null,
                    FoundBy = submission.Type == LostFoundType.Found ? submission.FoundBy?.Trim() : null,
                    GuestName = submission.Type == LostFoundType.Lost ? submission.GuestName?.Trim() : null,
                    GuestPhone = submission.Type == LostFoundType.Lost ? submission.GuestPhone?.Trim() : null,
                    GuestEmail = submission.Type == LostFoundType.Lost ? submission.GuestEmail?.Trim() : null,
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

                var departmentId = housekeepingDepartments.TryGetValue(propertyId, out var department)
                    ? department.Id
                    : fallbackHousekeepingDepartment?.Id;

                var workOrder = BuildLostFoundWorkOrder(entry, propertyId, departmentId, currentUser.Id);
                _context.WorkOrders.Add(workOrder);
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = Translate("Lost & Found entry logged successfully.");

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

            var entry = await _context.LostFoundEntries
                .Include(e => e.MatchedEntry)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (entry == null)
            {
                return NotFound();
            }

            if (!await HasPropertyAccessAsync(currentUser.Id, entry.PropertyId))
            {
                return Forbid();
            }

            entry.Status = status;

            LostFoundEntry? matchedEntry = null;
            if (entry.MatchedEntryId.HasValue)
            {
                matchedEntry = entry.MatchedEntry;
                if (matchedEntry == null)
                {
                    matchedEntry = await _context.LostFoundEntries
                        .FirstOrDefaultAsync(e => e.Id == entry.MatchedEntryId.Value);
                }

                if (matchedEntry != null)
                {
                    matchedEntry.Status = status;
                    _context.LostFoundEntries.Update(matchedEntry);
                }
            }

            _context.LostFoundEntries.Update(entry);
            await _context.SaveChangesAsync();

            TempData["Success"] = Translate("Entry status updated.");
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

            var entry = await _context.LostFoundEntries
                .Include(e => e.MatchedEntry)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (entry == null)
            {
                return NotFound();
            }

            if (!await HasPropertyAccessAsync(currentUser.Id, entry.PropertyId))
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

            await ClearExistingMatchAsync(entry);

            _context.LostFoundEntries.Remove(entry);
            await _context.SaveChangesAsync();

            TempData["Success"] = Translate("Lost & Found entry deleted.");
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditEntry(LostFoundEditInput input)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            var entry = await _context.LostFoundEntries
                .FirstOrDefaultAsync(e => e.Id == input.Id);

            if (entry == null)
            {
                return NotFound();
            }

            if (!await HasPropertyAccessAsync(currentUser.Id, entry.PropertyId))
            {
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                var firstError = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m));

                TempData["Error"] = !string.IsNullOrWhiteSpace(firstError)
                    ? Translate(firstError!, firstError)
                    : Translate("Unable to update entry. Please verify the information and try again.");
                return RedirectToAction(nameof(Index), new { propertyIds = new[] { entry.PropertyId } });
            }

            if (entry.Type == LostFoundType.Found)
            {
                if (!input.DateFound.HasValue)
                {
                    TempData["Error"] = Translate("Date Found is required for found items.");
                    return RedirectToAction(nameof(Index), new { propertyIds = new[] { entry.PropertyId } });
                }
                if (string.IsNullOrWhiteSpace(input.ItemFound))
                {
                    TempData["Error"] = Translate("Please provide a description of the found item.");
                    return RedirectToAction(nameof(Index), new { propertyIds = new[] { entry.PropertyId } });
                }

                entry.DateFound = NormalizeDateToUtc(input.DateFound);
                entry.ItemFound = input.ItemFound?.Trim();
                entry.FoundBy = string.IsNullOrWhiteSpace(input.FoundBy) ? null : input.FoundBy.Trim();
                entry.Stored = string.IsNullOrWhiteSpace(input.Stored) ? null : input.Stored.Trim();
                entry.DateReportedLost = null;
                entry.ItemLost = null;
                entry.GuestName = null;
                entry.GuestPhone = null;
                entry.GuestAddress = null;
                entry.GuestEmail = null;
            }
            else
            {
                if (!input.DateReportedLost.HasValue)
                {
                    TempData["Error"] = Translate("Date Reported Lost is required for lost items.");
                    return RedirectToAction(nameof(Index), new { propertyIds = new[] { entry.PropertyId } });
                }
                if (string.IsNullOrWhiteSpace(input.ItemLost))
                {
                    TempData["Error"] = Translate("Please provide a description of the lost item.");
                    return RedirectToAction(nameof(Index), new { propertyIds = new[] { entry.PropertyId } });
                }

                entry.DateReportedLost = NormalizeDateToUtc(input.DateReportedLost);
                entry.ItemLost = input.ItemLost?.Trim();
                entry.GuestName = string.IsNullOrWhiteSpace(input.GuestName) ? null : input.GuestName.Trim();
                entry.GuestPhone = string.IsNullOrWhiteSpace(input.GuestPhone) ? null : input.GuestPhone.Trim();
                entry.GuestAddress = string.IsNullOrWhiteSpace(input.GuestAddress) ? null : input.GuestAddress.Trim();
                entry.GuestEmail = string.IsNullOrWhiteSpace(input.GuestEmail) ? null : input.GuestEmail.Trim();
                entry.Stored = null;
                entry.FoundBy = null;
                entry.DateFound = null;
                entry.ItemFound = null;
            }

            entry.Location = string.IsNullOrWhiteSpace(input.Location) ? null : input.Location.Trim();
            entry.Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim();

            _context.LostFoundEntries.Update(entry);
            await _context.SaveChangesAsync();

            TempData["Success"] = Translate("Lost & Found entry updated.");
            return RedirectToAction(nameof(Index), new { propertyIds = new[] { entry.PropertyId } });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MatchEntries(int entryId, int matchId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            if (entryId == matchId)
            {
                TempData["Error"] = Translate("Please select a different entry to match.");
                return RedirectToAction(nameof(Index));
            }

            var entry = await _context.LostFoundEntries
                .Include(e => e.MatchedEntry)
                .FirstOrDefaultAsync(e => e.Id == entryId);
            var match = await _context.LostFoundEntries
                .Include(e => e.MatchedEntry)
                .FirstOrDefaultAsync(e => e.Id == matchId);

            if (entry == null || match == null)
            {
                return NotFound();
            }

            if (entry.PropertyId != match.PropertyId)
            {
                TempData["Error"] = Translate("Entries must belong to the same property.");
                return RedirectToAction(nameof(Index), new { propertyIds = new[] { entry.PropertyId } });
            }

            if (!await HasPropertyAccessAsync(currentUser.Id, entry.PropertyId))
            {
                return Forbid();
            }

            if (entry.Type == match.Type)
            {
                TempData["Error"] = Translate("Lost items can only be matched with found items.");
                return RedirectToAction(nameof(Index), new { propertyIds = new[] { entry.PropertyId } });
            }

            if (entry.Status != LostFoundStatus.Logged || match.Status != LostFoundStatus.Logged)
            {
                TempData["Error"] = Translate("Only logged entries can be matched.");
                return RedirectToAction(nameof(Index), new { propertyIds = new[] { entry.PropertyId } });
            }

            await ClearExistingMatchAsync(entry);
            await ClearExistingMatchAsync(match);

            entry.MatchedEntryId = match.Id;
            match.MatchedEntryId = entry.Id;

            _context.LostFoundEntries.Update(entry);
            _context.LostFoundEntries.Update(match);
            await _context.SaveChangesAsync();

            TempData["Success"] = Translate("Entries matched successfully.");
            return RedirectToAction(nameof(Index), new { propertyIds = new[] { entry.PropertyId } });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnmatchEntry(int id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            var entry = await _context.LostFoundEntries
                .Include(e => e.MatchedEntry)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (entry == null)
            {
                return NotFound();
            }

            if (!await HasPropertyAccessAsync(currentUser.Id, entry.PropertyId))
            {
                return Forbid();
            }

            if (!entry.MatchedEntryId.HasValue)
            {
                TempData["Success"] = Translate("Entry already unmatched.");
                return RedirectToAction(nameof(Index), new { propertyIds = new[] { entry.PropertyId } });
            }

            await ClearExistingMatchAsync(entry);
            _context.LostFoundEntries.Update(entry);
            await _context.SaveChangesAsync();

            TempData["Success"] = Translate("Entries unmatched.");
            return RedirectToAction(nameof(Index), new { propertyIds = new[] { entry.PropertyId } });
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

            var sessionPropertyId = HttpContext.Session.GetInt32("CurrentPropertyId");
            int? defaultPropertyId = null;
            if (sessionPropertyId.HasValue && accessiblePropertyIds.Contains(sessionPropertyId.Value))
            {
                defaultPropertyId = sessionPropertyId.Value;
            }
            else if (accessiblePropertyIds.Count == 1)
            {
                defaultPropertyId = accessiblePropertyIds.First();
            }

            var normalizedPropertyFilters = filters.PropertyIds
                .Where(id => accessiblePropertyIds.Contains(id))
                .Distinct()
                .ToList();

            if (!normalizedPropertyFilters.Any() && defaultPropertyId.HasValue)
            {
                normalizedPropertyFilters.Add(defaultPropertyId.Value);
            }

            filters.PropertyIds = normalizedPropertyFilters;

            var entriesQuery = _context.LostFoundEntries
                .Include(e => e.Property)
                .Include(e => e.CreatedByUser)
                .Include(e => e.MatchedEntry)
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

            if (filters.HideClosedItems)
            {
                filteredEntries = filteredEntries.Where(e =>
                    e.Status == LostFoundStatus.Logged);
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

            var sanitizedSelectedPropertyIds = (submission.SelectedPropertyIds ?? new List<int>())
                .Where(id => accessiblePropertyIds.Contains(id))
                .Distinct()
                .ToList();

            submission.SelectedPropertyIds = sanitizedSelectedPropertyIds;

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
                if (defaultPropertyId.HasValue)
                {
                    submission.SelectedPropertyIds = new List<int> { defaultPropertyId.Value };
                }
                else if (normalizedPropertyFilters.Any())
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
                MatchableFoundEntries = entries
                    .Where(e => e.Type == LostFoundType.Found && e.Status == LostFoundStatus.Logged)
                    .OrderByDescending(e => e.CreatedAt)
                    .ToList(),
                MatchableLostEntries = entries
                    .Where(e => e.Type == LostFoundType.Lost && e.Status == LostFoundStatus.Logged)
                    .OrderByDescending(e => e.CreatedAt)
                    .ToList(),
                AccessibleProperties = accessibleProperties,
                LocationOptions = locationOptions.OrderBy(x => x).ToList(),
                FoundByOptions = foundByOptions.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList(),
                CreatorOptions = creatorOptions
            };
        }

        private Task<bool> HasPropertyAccessAsync(string userId, int propertyId)
        {
            return _context.UserPropertyAccesses
                .AnyAsync(upa => upa.ApplicationUserId == userId && upa.PropertyId == propertyId);
        }

        private async Task ClearExistingMatchAsync(LostFoundEntry entry)
        {
            if (!entry.MatchedEntryId.HasValue)
            {
                return;
            }

            var counterpart = entry.MatchedEntry;
            if (counterpart == null)
            {
                counterpart = await _context.LostFoundEntries
                    .FirstOrDefaultAsync(e => e.Id == entry.MatchedEntryId.Value);
            }

            entry.MatchedEntryId = null;

            if (counterpart != null)
            {
                counterpart.MatchedEntryId = null;
                _context.LostFoundEntries.Update(counterpart);
            }
        }

        private static WorkOrder BuildLostFoundWorkOrder(LostFoundEntry entry, int propertyId, int? departmentId, string userId)
        {
            var workOrder = new WorkOrder
            {
                Status = WorkOrderStatusOptions.DefaultStatus,
                Issue = BuildLostFoundWorkOrderIssue(entry),
                Details = BuildLostFoundWorkOrderDetails(entry),
                Location = BuildLostFoundWorkOrderLocation(entry),
                CreatedAt = DateTime.UtcNow,
                DueDate = DateTime.UtcNow.Date,
                CreatedById = userId,
                DepartmentId = departmentId
            };

            workOrder.Properties.Add(new WorkOrderProperty
            {
                PropertyId = propertyId
            });

            return workOrder;
        }

        private static string BuildLostFoundWorkOrderIssue(LostFoundEntry entry)
        {
            var descriptor = GetLostFoundItemDescription(entry);
            var issue = $"Lost & Found - {descriptor}";

            return issue.Length <= 256 ? issue : issue.Substring(0, 256);
        }

        private static string BuildLostFoundWorkOrderLocation(LostFoundEntry entry)
        {
            var location = entry.Location?.Trim() ?? string.Empty;
            return location.Length <= 128 ? location : location.Substring(0, 128);
        }

        private static string? BuildLostFoundWorkOrderDetails(LostFoundEntry entry)
        {
            var builder = new StringBuilder();

            builder.AppendLine($"Entry Type: {(entry.Type == LostFoundType.Found ? "Found" : "Lost")}");
            var description = GetLostFoundItemDescription(entry);
            if (!string.IsNullOrWhiteSpace(description))
            {
                builder.AppendLine($"Item: {description}");
            }

            if (!string.IsNullOrWhiteSpace(entry.Location))
            {
                builder.AppendLine($"Location: {entry.Location}");
            }

            if (entry.Type == LostFoundType.Found)
            {
                if (entry.DateFound.HasValue)
                {
                    builder.AppendLine($"Date Found: {FormatDate(entry.DateFound)}");
                }

                if (!string.IsNullOrWhiteSpace(entry.FoundBy))
                {
                    builder.AppendLine($"Found By: {entry.FoundBy}");
                }
            }
            else
            {
                if (entry.DateReportedLost.HasValue)
                {
                    builder.AppendLine($"Date Reported Lost: {FormatDate(entry.DateReportedLost)}");
                }

                if (!string.IsNullOrWhiteSpace(entry.GuestName))
                {
                    builder.AppendLine($"Guest: {entry.GuestName}");
                }

                if (!string.IsNullOrWhiteSpace(entry.GuestPhone))
                {
                    builder.AppendLine($"Guest Phone: {entry.GuestPhone}");
                }

                if (!string.IsNullOrWhiteSpace(entry.GuestEmail))
                {
                    builder.AppendLine($"Guest Email: {entry.GuestEmail}");
                }
            }

            if (!string.IsNullOrWhiteSpace(entry.Stored))
            {
                builder.AppendLine($"Storage: {entry.Stored}");
            }

            if (!string.IsNullOrWhiteSpace(entry.Notes))
            {
                builder.AppendLine($"Notes: {entry.Notes}");
            }

            var details = builder.ToString().Trim();
            if (string.IsNullOrWhiteSpace(details))
            {
                return null;
            }

            return details.Length <= 2000 ? details : details.Substring(0, 2000);
        }

        private static string GetLostFoundItemDescription(LostFoundEntry entry)
        {
            var rawValue = entry.Type == LostFoundType.Found ? entry.ItemFound : entry.ItemLost;

            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return entry.Type == LostFoundType.Found ? "Found Item" : "Lost Item";
            }

            return rawValue.Trim();
        }

        private static string FormatDate(DateTime? value)
        {
            if (!value.HasValue)
            {
                return string.Empty;
            }

            return value.Value.ToString("yyyy-MM-dd");
        }

        private static DateTime? NormalizeDateToUtc(DateTime? value)
        {
            if (!value.HasValue)
            {
                return null;
            }

            var date = value.Value;
            return date.Kind switch
            {
                DateTimeKind.Utc => date,
                DateTimeKind.Local => date.ToUniversalTime(),
                _ => DateTime.SpecifyKind(date, DateTimeKind.Utc)
            };
        }
    }
}
