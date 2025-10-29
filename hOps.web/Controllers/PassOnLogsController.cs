using hOps.web.Data;
using hOps.web.Models;
using hOps.web.Services;
using hOps.web.Utilities;
using hOps.web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace hOps.web.Controllers
{
    [Authorize]
    public class PassOnLogsController : BaseController
    {
        private const string SortNewest = "newest";
        private const string SortOldest = "oldest";

        private readonly MentionService _mentionService;

        public PassOnLogsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, MentionService mentionService)
            : base(context, userManager)
        {
            _mentionService = mentionService;
        }

        public async Task<IActionResult> Index(string? sortOrder, DateTime? startDate, DateTime? endDate, string? creatorId, string? searchTerm)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            var accessibleProperties = await GetAccessiblePropertiesAsync(currentUser.Id);
            var accessiblePropertyIds = accessibleProperties.Select(p => p.Id).ToList();

            var filters = new PassOnLogFiltersViewModel
            {
                SortOrder = string.IsNullOrWhiteSpace(sortOrder) ? SortNewest : sortOrder,
                StartDate = startDate,
                EndDate = endDate,
                CreatorId = creatorId,
                SearchTerm = searchTerm
            };

            filters.SortOptions = new List<SelectListItem>
            {
                new SelectListItem
                {
                    Value = SortNewest,
                    Text = "Newest",
                    Selected = filters.SortOrder == SortNewest
                },
                new SelectListItem
                {
                    Value = SortOldest,
                    Text = "Oldest",
                    Selected = filters.SortOrder == SortOldest
                }
            };

            IQueryable<PassOnLog> baseQuery = _context.PassOnLogs;

            if (accessiblePropertyIds.Any())
            {
                baseQuery = baseQuery.Where(log => log.Properties.Any(lp => accessiblePropertyIds.Contains(lp.PropertyId)));
            }
            else
            {
                baseQuery = baseQuery.Where(_ => false);
            }

            var creatorIds = await baseQuery
                .Select(log => log.CreatedById)
                .Distinct()
                .ToListAsync();

            var creatorUsers = await _userManager.Users
                .Where(u => creatorIds.Contains(u.Id))
                .ToListAsync();

            filters.CreatorOptions = new List<SelectListItem>
            {
                new SelectListItem
                {
                    Value = string.Empty,
                    Text = "All Creators",
                    Selected = string.IsNullOrEmpty(filters.CreatorId)
                }
            };

            foreach (var creator in creatorUsers.OrderBy(c => c.FirstName).ThenBy(c => c.LastName))
            {
                var name = FormatUserName(creator.FirstName, creator.LastName, creator.Email ?? string.Empty);
                filters.CreatorOptions.Add(new SelectListItem
                {
                    Text = name,
                    Value = creator.Id,
                    Selected = creator.Id == filters.CreatorId
                });
            }

            IQueryable<PassOnLog> logsQuery = baseQuery;
            logsQuery = logsQuery.Include(log => log.CreatedBy);
            logsQuery = logsQuery.Include(log => log.Properties).ThenInclude(lp => lp.Property);
            logsQuery = logsQuery.Include(log => log.Comments);
            logsQuery = logsQuery.Include(log => log.Views);

            if (filters.StartDate.HasValue)
            {
                var from = DateTime.SpecifyKind(filters.StartDate.Value.Date, DateTimeKind.Utc);
                logsQuery = logsQuery.Where(log => log.CreatedAt >= from);
            }

            if (filters.EndDate.HasValue)
            {
                var to = DateTime.SpecifyKind(filters.EndDate.Value.Date.AddDays(1), DateTimeKind.Utc);
                logsQuery = logsQuery.Where(log => log.CreatedAt < to);
            }

            if (!string.IsNullOrEmpty(filters.CreatorId))
            {
                logsQuery = logsQuery.Where(log => log.CreatedById == filters.CreatorId);
            }

            if (!string.IsNullOrWhiteSpace(filters.SearchTerm))
            {
                var term = filters.SearchTerm.Trim();
                logsQuery = logsQuery.Where(log => EF.Functions.Like(log.Title, $"%{term}%") || EF.Functions.Like(log.Body, $"%{term}%"));
            }

            logsQuery = filters.SortOrder == SortOldest
                ? logsQuery.OrderBy(log => log.CreatedAt)
                : logsQuery.OrderByDescending(log => log.CreatedAt);

            var logs = await logsQuery.AsNoTracking().ToListAsync();

            var logItems = logs.Select(log => new PassOnLogListItemViewModel
            {
                Id = log.Id,
                Title = log.Title,
                CreatorName = FormatUserName(log.CreatedBy?.FirstName, log.CreatedBy?.LastName, log.CreatedBy?.Email ?? string.Empty),
                CreatedAt = log.CreatedAt,
                IsUnread = IsLogUnread(log, currentUser.Id),
                PropertyNames = log.Properties.Select(lp => lp.Property.Name).Distinct().OrderBy(name => name).ToList(),
                CommentCount = log.Comments.Count,
                Preview = BuildPreview(log.Body)
            }).ToList();

            var model = new PassOnLogIndexViewModel
            {
                Logs = logItems,
                Filters = filters,
                CanCreateLog = accessibleProperties.Any()
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            var accessibleProperties = await GetAccessiblePropertiesAsync(currentUser.Id);
            if (!accessibleProperties.Any())
            {
                TempData["ErrorMessage"] = "You do not have access to any properties.";
                return RedirectToAction(nameof(Index));
            }

            var model = BuildFormViewModel(new PassOnLogFormViewModel(), accessibleProperties);
            if (accessibleProperties.Count == 1)
            {
                model.SelectedPropertyIds = new List<int> { accessibleProperties.First().Id };
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PassOnLogFormViewModel model)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            var accessibleProperties = await GetAccessiblePropertiesAsync(currentUser.Id);
            if (!accessibleProperties.Any())
            {
                TempData["ErrorMessage"] = "You do not have access to any properties.";
                return RedirectToAction(nameof(Index));
            }

            model = BuildFormViewModel(model, accessibleProperties);

            EnsurePropertySelection(model, accessibleProperties.Select(p => p.Id).ToList());

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var log = new PassOnLog
            {
                Title = model.Title.Trim(),
                Body = model.Body.Trim(),
                CreatedAt = DateTime.UtcNow,
                CreatedById = currentUser.Id
            };

            foreach (var propertyId in model.SelectedPropertyIds)
            {
                log.Properties.Add(new PassOnLogProperty
                {
                    PropertyId = propertyId
                });
            }

            _context.PassOnLogs.Add(log);
            await _context.SaveChangesAsync();

            var link = Url.Action(nameof(Details), "PassOnLogs", new { id = log.Id }, Request.Scheme)
                ?? Url.Action(nameof(Index), "PassOnLogs") ?? "/PassOnLogs";

            await _mentionService.CreateMentionNotificationsAsync(
                log.Body,
                currentUser,
                $"Pass On Log: {log.Title}",
                link,
                log.Body);

            return RedirectToAction(nameof(Details), new { id = log.Id });
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            var log = await _context.PassOnLogs
                .Include(l => l.Properties)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (log == null)
            {
                return NotFound();
            }

            if (log.CreatedById != currentUser.Id)
            {
                return Forbid();
            }

            var accessibleProperties = await GetAccessiblePropertiesAsync(currentUser.Id);
            if (!accessibleProperties.Any())
            {
                TempData["ErrorMessage"] = "You do not have access to any properties.";
                return RedirectToAction(nameof(Index));
            }

            var model = new PassOnLogFormViewModel
            {
                Id = log.Id,
                Title = log.Title,
                Body = log.Body,
                SelectedPropertyIds = log.Properties.Select(p => p.PropertyId).ToList()
            };

            model = BuildFormViewModel(model, accessibleProperties);

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, PassOnLogFormViewModel model)
        {
            if (id != model.Id)
            {
                return BadRequest();
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            var log = await _context.PassOnLogs
                .Include(l => l.Properties)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (log == null)
            {
                return NotFound();
            }

            if (log.CreatedById != currentUser.Id)
            {
                return Forbid();
            }

            var accessibleProperties = await GetAccessiblePropertiesAsync(currentUser.Id);
            if (!accessibleProperties.Any())
            {
                TempData["ErrorMessage"] = "You do not have access to any properties.";
                return RedirectToAction(nameof(Index));
            }

            model = BuildFormViewModel(model, accessibleProperties);

            EnsurePropertySelection(model, accessibleProperties.Select(p => p.Id).ToList());

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            log.Title = model.Title.Trim();
            log.Body = model.Body.Trim();
            log.UpdatedAt = DateTime.UtcNow;

            var selectedIds = model.SelectedPropertyIds.Distinct().ToList();
            var existing = log.Properties.ToList();

            foreach (var relation in existing.Where(r => !selectedIds.Contains(r.PropertyId)))
            {
                _context.PassOnLogProperties.Remove(relation);
            }

            foreach (var propertyId in selectedIds)
            {
                if (!existing.Any(r => r.PropertyId == propertyId))
                {
                    log.Properties.Add(new PassOnLogProperty
                    {
                        PropertyId = propertyId
                    });
                }
            }

            await _context.SaveChangesAsync();

            var link = Url.Action(nameof(Details), "PassOnLogs", new { id = log.Id }, Request.Scheme)
                ?? Url.Action(nameof(Index), "PassOnLogs") ?? "/PassOnLogs";

            await _mentionService.CreateMentionNotificationsAsync(
                log.Body,
                currentUser,
                $"Pass On Log: {log.Title}",
                link,
                log.Body);

            return RedirectToAction(nameof(Details), new { id = log.Id });
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            var log = await _context.PassOnLogs
                .Include(l => l.CreatedBy)
                .Include(l => l.Properties).ThenInclude(lp => lp.Property)
                .Include(l => l.Comments).ThenInclude(c => c.CreatedBy)
                .Include(l => l.Views).ThenInclude(v => v.Viewer)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (log == null)
            {
                return NotFound();
            }

            var accessiblePropertyIds = (await GetAccessiblePropertiesAsync(currentUser.Id)).Select(p => p.Id).ToList();
            if (!log.Properties.Any(p => accessiblePropertyIds.Contains(p.PropertyId)))
            {
                return Forbid();
            }

            if (log.CreatedById != currentUser.Id && !log.Views.Any(v => v.ViewerId == currentUser.Id))
            {
                log.Views.Add(new PassOnLogView
                {
                    PassOnLogId = log.Id,
                    ViewerId = currentUser.Id,
                    Viewer = currentUser,
                    ViewedAt = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
            }

            var model = BuildDetailsViewModel(log, currentUser.Id);

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddComment([Bind(Prefix = "NewComment")] PassOnLogCommentInputModel input)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            var log = await _context.PassOnLogs
                .Include(l => l.CreatedBy)
                .Include(l => l.Properties).ThenInclude(lp => lp.Property)
                .Include(l => l.Comments).ThenInclude(c => c.CreatedBy)
                .Include(l => l.Views).ThenInclude(v => v.Viewer)
                .FirstOrDefaultAsync(l => l.Id == input.LogId);

            if (log == null)
            {
                return NotFound();
            }

            var accessiblePropertyIds = (await GetAccessiblePropertiesAsync(currentUser.Id)).Select(p => p.Id).ToList();
            if (!log.Properties.Any(p => accessiblePropertyIds.Contains(p.PropertyId)))
            {
                return Forbid();
            }

            input.Body = input.Body?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(input.Body))
            {
                ModelState.AddModelError("NewComment.Body", "Comment cannot be empty.");
            }

            if (!ModelState.IsValid)
            {
                var modelWithErrors = BuildDetailsViewModel(log, currentUser.Id);
                modelWithErrors.NewComment = input;
                return View("Details", modelWithErrors);
            }

            var comment = new PassOnLogComment
            {
                PassOnLogId = log.Id,
                Body = input.Body,
                CreatedAt = DateTime.UtcNow,
                CreatedById = currentUser.Id
            };

            _context.PassOnLogComments.Add(comment);
            await _context.SaveChangesAsync();

            var link = Url.Action(nameof(Details), "PassOnLogs", new { id = log.Id }, Request.Scheme)
                ?? Url.Action(nameof(Index), "PassOnLogs") ?? "/PassOnLogs";

            await _mentionService.CreateMentionNotificationsAsync(
                comment.Body,
                currentUser,
                $"Pass On Log Comment: {log.Title}",
                link,
                comment.Body);

            return RedirectToAction(nameof(Details), new { id = log.Id });
        }

        private static string BuildPreview(string body)
        {
            var preview = MentionMarkupFormatter.ToDisplayText(body ?? string.Empty)
                .ReplaceLineEndings(" ")
                .Trim();

            if (string.IsNullOrWhiteSpace(preview))
            {
                return string.Empty;
            }

            return preview.Length <= 180
                ? preview
                : $"{preview[..180]}…";
        }

        private async Task<List<Property>> GetAccessiblePropertiesAsync(string userId)
        {
            return await _context.UserPropertyAccesses
                .Where(upa => upa.ApplicationUserId == userId)
                .Include(upa => upa.Property)
                .Select(upa => upa.Property)
                .Distinct()
                .OrderBy(p => p.Name)
                .ToListAsync();
        }

        private PassOnLogFormViewModel BuildFormViewModel(PassOnLogFormViewModel model, List<Property> accessibleProperties)
        {
            model.PropertyOptions = accessibleProperties
                .Select(p => new PassOnLogPropertyOptionViewModel
                {
                    Id = p.Id,
                    Name = p.Name,
                    Code = p.Code
                })
                .ToList();

            model.SelectedPropertyIds ??= new List<int>();

            return model;
        }

        private void EnsurePropertySelection(PassOnLogFormViewModel model, List<int> allowedPropertyIds)
        {
            model.SelectedPropertyIds = model.SelectedPropertyIds
                .Where(id => allowedPropertyIds.Contains(id))
                .Distinct()
                .ToList();

            if (!model.SelectedPropertyIds.Any() && allowedPropertyIds.Count == 1)
            {
                model.SelectedPropertyIds = new List<int> { allowedPropertyIds.First() };
            }

            if (!model.SelectedPropertyIds.Any())
            {
                ModelState.AddModelError(nameof(model.SelectedPropertyIds), "Please select at least one property.");
            }
        }

        private PassOnLogDetailsViewModel BuildDetailsViewModel(PassOnLog log, string currentUserId)
        {
            var vm = new PassOnLogDetailsViewModel
            {
                Id = log.Id,
                Title = log.Title,
                Body = log.Body,
                CreatorName = FormatUserName(log.CreatedBy?.FirstName, log.CreatedBy?.LastName, log.CreatedBy?.Email ?? string.Empty),
                CreatedAt = log.CreatedAt,
                UpdatedAt = log.UpdatedAt,
                PropertyNames = log.Properties.Select(lp => lp.Property.Name).Distinct().OrderBy(name => name).ToList(),
                CanEdit = log.CreatedById == currentUserId,
                Comments = log.Comments
                    .OrderBy(c => c.CreatedAt)
                    .Select(c => new PassOnLogCommentViewModel
                    {
                        Id = c.Id,
                        Body = c.Body,
                        CreatedAt = c.CreatedAt,
                        CreatorName = FormatUserName(c.CreatedBy?.FirstName, c.CreatedBy?.LastName, c.CreatedBy?.Email ?? string.Empty)
                    })
                    .ToList(),
                Viewers = log.Views
                    .OrderByDescending(v => v.ViewedAt)
                    .Select(v => new PassOnLogViewerViewModel
                    {
                        Name = FormatUserName(v.Viewer?.FirstName, v.Viewer?.LastName, v.Viewer?.Email ?? string.Empty),
                        ViewedAt = v.ViewedAt
                    })
                    .ToList(),
                NewComment = new PassOnLogCommentInputModel
                {
                    LogId = log.Id
                }
            };

            return vm;
        }

        private static bool IsLogUnread(PassOnLog log, string userId)
        {
            if (log.CreatedById == userId)
            {
                return false;
            }

            return !log.Views.Any(v => v.ViewerId == userId);
        }

        private static string FormatUserName(string? firstName, string? lastName, string email)
        {
            var name = ($"{firstName} {lastName}").Trim();
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            return string.IsNullOrWhiteSpace(email) ? "Unknown User" : email;
        }
    }
}






