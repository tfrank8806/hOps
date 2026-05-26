using hOps.web.Data;
using hOps.web.Models;
using hOps.web.Services;
using hOps.web.Utilities;
using hOps.web.ViewModels;
using hOps.web.Services.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace hOps.web.Controllers
{
    [Authorize]
    public class PassOnLogsController : BaseController
    {
        private const string SortNewest = "newest";
        private const string SortOldest = "oldest";

        private readonly MentionService _mentionService;
        private readonly IPassOnLogNotificationService _notificationService;
        private readonly ILogger<PassOnLogsController> _logger;
        private readonly IWebHostEnvironment _environment;
        private readonly ITranslationService _translationService;

        public PassOnLogsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            MentionService mentionService,
            IPassOnLogNotificationService notificationService,
            ILogger<PassOnLogsController> logger,
            IWebHostEnvironment environment,
            ITranslationService translationService)
            : base(context, userManager)
        {
            _mentionService = mentionService;
            _notificationService = notificationService;
            _logger = logger;
            _environment = environment;
            _translationService = translationService;
        }

        public async Task<IActionResult> Index([FromQuery] PassOnLogFiltersViewModel? filters)
        {
            var logActiveLanguage = HttpContext.Items["ActiveLanguage"] as string ?? "(null)";
            _logger.LogInformation(
                "LANGDEBUG PassOnLogs/Index culture={Culture} uiCulture={UICulture} active={ActiveLanguage} defaultLang={DefaultLanguage}",
                CultureInfo.CurrentCulture.Name,
                CultureInfo.CurrentUICulture.Name,
                logActiveLanguage,
                _translationService.DefaultLanguage);

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            filters ??= new PassOnLogFiltersViewModel();
            filters.Normalize();

            var accessibleProperties = await GetAccessiblePropertiesAsync(currentUser.Id);
            var accessiblePropertyIds = accessibleProperties.Select(p => p.Id).ToList();

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

            filters.PropertyIds = filters.PropertyIds
                .Where(id => accessiblePropertyIds.Contains(id))
                .Distinct()
                .ToList();

            IQueryable<PassOnLog> baseQuery = _context.PassOnLogs;

            if (accessiblePropertyIds.Any())
            {
                baseQuery = baseQuery.Where(log => log.Properties.Any(lp => accessiblePropertyIds.Contains(lp.PropertyId)));
            }
            else
            {
                baseQuery = baseQuery.Where(_ => false);
            }

            if (filters.PropertyIds.Any())
            {
                baseQuery = baseQuery.Where(log => log.Properties.Any(lp => filters.PropertyIds.Contains(lp.PropertyId)));
            }

            var creatorIds = await baseQuery
                .Select(log => log.CreatedById)
                .Where(id => id != null)
                .Distinct()
                .ToListAsync();

            var creatorUsers = await _userManager.Users
                .Where(u => creatorIds.Contains(u.Id))
                .ToListAsync();

            var creatorFilterSet = new HashSet<string>(filters.CreatorIds, StringComparer.OrdinalIgnoreCase);

            filters.CreatorOptions = creatorUsers
                .OrderBy(c => c.FirstName)
                .ThenBy(c => c.LastName)
                .Select(creator => new SelectListItem
                {
                    Text = PassOnLogEmailHelper.FormatUserName(creator.FirstName, creator.LastName, creator.Email ?? string.Empty),
                    Value = creator.Id,
                    Selected = creatorFilterSet.Contains(creator.Id)
                })
                .ToList();

            filters.PropertyOptions = accessibleProperties
                .Select(p => new PassOnLogPropertyOptionViewModel
                {
                    Id = p.Id,
                    Name = p.Name,
                    TranslatedName = p.Name,
                    Code = p.Code,
                    IsSelected = filters.PropertyIds.Contains(p.Id)
                })
                .ToList();

            IQueryable<PassOnLog> logsQuery = baseQuery;
            logsQuery = logsQuery.Include(log => log.CreatedBy);
            logsQuery = logsQuery.Include(log => log.Properties).ThenInclude(lp => lp.Property);
            logsQuery = logsQuery.Include(log => log.Comments);
            logsQuery = logsQuery.Include(log => log.Views);
            logsQuery = logsQuery.AsSplitQuery();

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

            if (creatorFilterSet.Any())
            {
                logsQuery = logsQuery.Where(log => log.CreatedById != null && creatorFilterSet.Contains(log.CreatedById));
            }

            if (!string.IsNullOrEmpty(filters.SearchTerm))
            {
                var term = filters.SearchTerm!;
                logsQuery = logsQuery.Where(log => EF.Functions.Like(log.Title, $"%{term}%") || EF.Functions.Like(log.Body, $"%{term}%"));
            }

            logsQuery = filters.SortOrder == SortOldest
                ? logsQuery.OrderBy(log => log.CreatedAt)
                : logsQuery.OrderByDescending(log => log.CreatedAt);

            var logs = await logsQuery.AsNoTracking().ToListAsync();

            var logItems = logs.Select(log =>
            {
                var creatorName = PassOnLogEmailHelper.FormatUserName(log.CreatedBy?.FirstName, log.CreatedBy?.LastName, log.CreatedBy?.Email ?? string.Empty);

                var properties = log.Properties
                    .Where(lp => lp.Property != null)
                    .GroupBy(lp => lp.PropertyId)
                    .Select(group =>
                    {
                        var property = group.First().Property;
                        return new PassOnLogPropertyDisplayViewModel
                        {
                            Id = group.Key,
                            Name = property?.Name ?? string.Empty,
                            TranslatedName = property?.Name ?? string.Empty,
                            Code = property?.Code ?? string.Empty
                        };
                    })
                    .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return new PassOnLogListItemViewModel
                {
                    Id = log.Id,
                    Title = log.Title,
                    TranslatedTitle = log.Title,
                    CreatorName = creatorName,
                    CreatorAvatar = UserAvatarHelper.BuildFromUser(log.CreatedBy, creatorName, "lg"),
                    CreatedAt = log.CreatedAt,
                    IsUnread = IsLogUnread(log, currentUser.Id),
                    PropertyNames = properties.Select(p => p.Name).Where(name => !string.IsNullOrWhiteSpace(name)).Distinct().OrderBy(name => name).ToList(),
                    TranslatedPropertyNames = properties
                        .Select(p =>
                        {
                            var name = string.IsNullOrWhiteSpace(p.TranslatedName) ? p.Name : p.TranslatedName;
                            if (string.IsNullOrWhiteSpace(name))
                            {
                                return null;
                            }

                            return string.IsNullOrWhiteSpace(p.Code)
                                ? name
                                : string.Format(CultureInfo.CurrentCulture, "{0} ({1})", name, p.Code);
                        })
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .ToList()!,
                    Properties = properties,
                    CommentCount = log.Comments.Count,
                    Preview = BuildPreview(log.Body),
                    TranslatedPreview = BuildPreview(log.Body)
                };
            }).ToList();

            var activeLanguage = HttpContext.Items["ActiveLanguage"] as string ?? _translationService.DefaultLanguage;
            var isDefaultLanguage = string.Equals(activeLanguage, _translationService.DefaultLanguage, StringComparison.OrdinalIgnoreCase);
            var cancellationToken = HttpContext.RequestAborted;

            string? BuildPropertyDisplayName(PassOnLogPropertyDisplayViewModel property)
            {
                var displayName = string.IsNullOrWhiteSpace(property.TranslatedName) ? property.Name : property.TranslatedName;
                if (string.IsNullOrWhiteSpace(displayName))
                {
                    return null;
                }

                return string.IsNullOrWhiteSpace(property.Code)
                    ? displayName
                    : string.Format(CultureInfo.CurrentCulture, "{0} ({1})", displayName, property.Code);
            }

            if (!isDefaultLanguage)
            {
                foreach (var item in logItems)
                {
                    var entityId = item.Id.ToString(CultureInfo.InvariantCulture);

                    if (!string.IsNullOrWhiteSpace(item.Title))
                    {
                        var translatedTitle = await _translationService.TranslateDynamicAsync(
                            "PassOnLog",
                            entityId,
                            "Title",
                            item.Title,
                            _translationService.DefaultLanguage,
                            activeLanguage,
                            cancellationToken);
                        if (!string.IsNullOrWhiteSpace(translatedTitle))
                        {
                            item.TranslatedTitle = translatedTitle;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(item.Preview))
                    {
                        var translatedPreview = await _translationService.TranslateDynamicAsync(
                            "PassOnLog",
                            entityId,
                            "Preview",
                            item.Preview,
                            _translationService.DefaultLanguage,
                            activeLanguage,
                            cancellationToken);
                        if (!string.IsNullOrWhiteSpace(translatedPreview))
                        {
                            item.TranslatedPreview = translatedPreview;
                        }
                    }

                    if (item.Properties is { Count: > 0 })
                    {
                        foreach (var property in item.Properties)
                        {
                            if (string.IsNullOrWhiteSpace(property.Name))
                            {
                                property.TranslatedName = property.Name;
                                continue;
                            }

                            var translatedPropertyName = await _translationService.TranslateDynamicAsync(
                                "Property",
                                property.Id.ToString(CultureInfo.InvariantCulture),
                                "Name",
                                property.Name,
                                _translationService.DefaultLanguage,
                                activeLanguage,
                                cancellationToken);

                            property.TranslatedName = string.IsNullOrWhiteSpace(translatedPropertyName)
                                ? property.Name
                                : translatedPropertyName;
                        }

                        item.TranslatedPropertyNames = item.Properties
                            .Select(BuildPropertyDisplayName)
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .ToList()!;
                    }
                }

                if (filters.PropertyOptions != null && filters.PropertyOptions.Count > 0)
                {
                    foreach (var option in filters.PropertyOptions)
                    {
                        if (string.IsNullOrWhiteSpace(option.Name))
                        {
                            option.TranslatedName = option.Name;
                            continue;
                        }

                        var translatedName = await _translationService.TranslateDynamicAsync(
                            "Property",
                            option.Id.ToString(CultureInfo.InvariantCulture),
                            "Name",
                            option.Name,
                            _translationService.DefaultLanguage,
                            activeLanguage,
                            cancellationToken);

                        option.TranslatedName = string.IsNullOrWhiteSpace(translatedName)
                            ? option.Name
                            : translatedName;
                    }
                }
            }
            else
            {
                foreach (var item in logItems)
                {
                    item.TranslatedTitle = item.Title;
                    item.TranslatedPreview = item.Preview;
                    if (item.Properties is { Count: > 0 })
                    {
                        foreach (var property in item.Properties)
                        {
                            property.TranslatedName = property.Name;
                        }

                        item.TranslatedPropertyNames = item.Properties
                            .Select(BuildPropertyDisplayName)
                            .Where(value => !string.IsNullOrWhiteSpace(value))
                            .ToList()!;
                    }
                }

                if (filters.PropertyOptions != null && filters.PropertyOptions.Count > 0)
                {
                    foreach (var option in filters.PropertyOptions)
                    {
                        option.TranslatedName = option.Name;
                    }
                }
            }

            if (filters.ShowUnreadOnly)
            {
                logItems = logItems.Where(item => item.IsUnread).ToList();
            }

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

            var model = await BuildFormViewModelAsync(new PassOnLogFormViewModel(), accessibleProperties);
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

            model = await BuildFormViewModelAsync(model, accessibleProperties);

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

            var uploadedAttachments = await SaveAttachmentsAsync(model.Files);
            foreach (var upload in uploadedAttachments)
            {
                log.Attachments.Add(upload.Attachment);
            }

            _context.PassOnLogs.Add(log);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch
            {
                foreach (var upload in uploadedAttachments)
                {
                    DeletePhysicalFile(upload.PhysicalPath);
                }

                throw;
            }

            var link = Url.Action(nameof(Details), "PassOnLogs", new { id = log.Id }, Request.Scheme)
                ?? Url.Action(nameof(Index), "PassOnLogs") ?? "/PassOnLogs";

            await _mentionService.CreateMentionNotificationsAsync(
                log.Body,
                currentUser,
                $"Pass On Log: {log.Title}",
                link,
                log.Body);

            var logAlertRecipients = await _notificationService.GetLogEntryAlertRecipientsAsync(log, currentUser);
            await _notificationService.NotifyLogSubscribersAsync(log, currentUser, link, logAlertRecipients);
            await _notificationService.SendLogEntryEmailsAsync(log, currentUser, link, logAlertRecipients);

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
                .Include(l => l.Attachments)
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
                SelectedPropertyIds = log.Properties.Select(p => p.PropertyId).ToList(),
                ExistingAttachments = log.Attachments
                    .Select(a => new PassOnLogAttachmentViewModel
                    {
                        Id = a.Id,
                        FileName = GetAttachmentDisplayName(a),
                        DownloadUrl = a.FilePath
                    })
                    .ToList()
            };

            model = await BuildFormViewModelAsync(model, accessibleProperties);

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
                .Include(l => l.Attachments)
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

            var existingAttachments = log.Attachments
                .Select(a => new PassOnLogAttachmentViewModel
                {
                    Id = a.Id,
                    FileName = GetAttachmentDisplayName(a),
                    DownloadUrl = a.FilePath
                })
                .ToList();

            model.ExistingAttachments = existingAttachments;

            model = await BuildFormViewModelAsync(model, accessibleProperties);

            EnsurePropertySelection(model, accessibleProperties.Select(p => p.Id).ToList());

            if (!ModelState.IsValid)
            {
                model.ExistingAttachments = existingAttachments;
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

            var attachmentsMarkedForDeletion = new List<string>();
            if (model.AttachmentsToDelete != null && model.AttachmentsToDelete.Any())
            {
                var toRemove = log.Attachments
                    .Where(a => model.AttachmentsToDelete.Contains(a.Id))
                    .ToList();

                foreach (var attachment in toRemove)
                {
                    attachmentsMarkedForDeletion.Add(GetPhysicalPathForAttachment(attachment.FilePath));
                    log.Attachments.Remove(attachment);
                    _context.PassOnLogAttachments.Remove(attachment);
                }
            }

            var uploadedAttachments = await SaveAttachmentsAsync(model.Files);
            foreach (var upload in uploadedAttachments)
            {
                log.Attachments.Add(upload.Attachment);
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch
            {
                foreach (var upload in uploadedAttachments)
                {
                    DeletePhysicalFile(upload.PhysicalPath);
                }

                throw;
            }

            foreach (var path in attachmentsMarkedForDeletion)
            {
                DeletePhysicalFile(path);
            }

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
                .AsSplitQuery()
                .Include(l => l.CreatedBy)
                .Include(l => l.Properties).ThenInclude(lp => lp.Property)
                .Include(l => l.Comments).ThenInclude(c => c.CreatedBy)
                .Include(l => l.Views).ThenInclude(v => v.Viewer)
                .Include(l => l.Attachments)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (log == null)
            {
                return NotFound();
            }

            var accessiblePropertyIds = (await GetAccessiblePropertiesAsync(currentUser.Id))
                .Select(p => p.Id)
                .ToList();
            if (!log.Properties.Any(p => accessiblePropertyIds.Contains(p.PropertyId)))
            {
                return Forbid();
            }

            var (nextLogId, previousLogId) = await GetNeighborLogIdsAsync(log, accessiblePropertyIds);

            var hasChanges = false;

            if (log.CreatedById != currentUser.Id && !log.Views.Any(v => v.ViewerId == currentUser.Id))
            {
                log.Views.Add(new PassOnLogView
                {
                    PassOnLogId = log.Id,
                    ViewerId = currentUser.Id,
                    Viewer = currentUser,
                    ViewedAt = DateTime.UtcNow
                });
                hasChanges = true;
            }

            if (await MarkPassOnLogAlertsAsReadAsync(log.Id, currentUser.Id))
            {
                hasChanges = true;
            }

            if (hasChanges)
            {
                await _context.SaveChangesAsync();
            }

            var canDelete = User.IsInRole("Admin") || User.IsInRole("Manager");
            var model = await BuildDetailsViewModelAsync(log, currentUser.Id, nextLogId, previousLogId, canDelete);
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
                .AsSplitQuery()
                .Include(l => l.CreatedBy)
                .Include(l => l.Properties).ThenInclude(lp => lp.Property)
                .Include(l => l.Attachments)
                .Include(l => l.Comments).ThenInclude(c => c.CreatedBy)
                .Include(l => l.Views).ThenInclude(v => v.Viewer)
                .FirstOrDefaultAsync(l => l.Id == input.LogId);

            if (log == null)
            {
                return NotFound();
            }

            var accessiblePropertyIds = (await GetAccessiblePropertiesAsync(currentUser.Id))
                .Select(p => p.Id)
                .ToList();
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
                var (nextLogId, previousLogId) = await GetNeighborLogIdsAsync(log, accessiblePropertyIds);
                var canDelete = User.IsInRole("Admin") || User.IsInRole("Manager");
                var modelWithErrors = await BuildDetailsViewModelAsync(log, currentUser.Id, nextLogId, previousLogId, canDelete);
                modelWithErrors.NewComment = input;
                modelWithErrors.NewComment.ReturnUrl = input.ReturnUrl;
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
            log.Comments.Add(comment);
            comment.CreatedBy = currentUser;
            MarkLogUnreadForUnseenCommentViewers(log, currentUser.Id, comment.CreatedAt);
            var uploadedAttachments = await SaveAttachmentsAsync(input.Files);
            if (uploadedAttachments.Count > 0)
            {
                log.Attachments ??= new List<PassOnLogAttachment>();
                foreach (var upload in uploadedAttachments)
                {
                    log.Attachments.Add(upload.Attachment);
                }
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch
            {
                foreach (var upload in uploadedAttachments)
                {
                    DeletePhysicalFile(upload.PhysicalPath);
                }

                throw;
            }

            var link = Url.Action(nameof(Details), "PassOnLogs", new { id = log.Id }, Request.Scheme)
                ?? Url.Action(nameof(Index), "PassOnLogs") ?? "/PassOnLogs";

            await _mentionService.CreateMentionNotificationsAsync(
                comment.Body,
                currentUser,
                $"Pass On Log Comment: {log.Title}",
                link,
                comment.Body);

            var logAlertRecipients = await _notificationService.GetLogEntryAlertRecipientsAsync(log, currentUser);
            await _notificationService.SendLogCommentEmailsAsync(log, comment, currentUser, link, logAlertRecipients);

            if (!string.IsNullOrWhiteSpace(input.ReturnUrl) && Url.IsLocalUrl(input.ReturnUrl))
            {
                return Redirect(input.ReturnUrl);
            }

            return RedirectToAction(nameof(Details), new { id = log.Id });
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

            var canManage = User.IsInRole("Admin") || User.IsInRole("Manager");
            if (!canManage)
            {
                return Forbid();
            }

            var log = await _context.PassOnLogs
                .AsSplitQuery()
                .Include(l => l.Properties)
                .Include(l => l.Attachments)
                .Include(l => l.Comments)
                .Include(l => l.Views)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (log == null)
            {
                return NotFound();
            }

            if (!User.IsInRole("Admin"))
            {
                var accessiblePropertyIds = (await GetAccessiblePropertiesAsync(currentUser.Id))
                    .Select(p => p.Id)
                    .ToList();

                if (!log.Properties.Any(p => accessiblePropertyIds.Contains(p.PropertyId)))
                {
                    return Forbid();
                }
            }

            var attachmentPaths = log.Attachments
                .Select(a => GetPhysicalPathForAttachment(a.FilePath))
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .ToList();

            var notifications = await _context.UserNotifications
                .Where(n => n.PassOnLogId == log.Id)
                .ToListAsync();

            _context.PassOnLogComments.RemoveRange(log.Comments);
            _context.PassOnLogAttachments.RemoveRange(log.Attachments);
            _context.PassOnLogViews.RemoveRange(log.Views);
            _context.PassOnLogProperties.RemoveRange(log.Properties);
            if (notifications.Any())
            {
                _context.UserNotifications.RemoveRange(notifications);
            }

            _context.PassOnLogs.Remove(log);
            await _context.SaveChangesAsync();

            foreach (var path in attachmentPaths)
            {
                DeletePhysicalFile(path);
            }

            TempData["Success"] = "Pass on log deleted.";
            return RedirectToAction(nameof(Index));
        }

        private static string BuildPreview(string body)
        {
            var preview = RichTextRenderer.ToPlainText(body ?? string.Empty)
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
            var properties = await _context.UserPropertyAccesses
                .Where(upa => upa.ApplicationUserId == userId)
                .Include(upa => upa.Property)
                .Select(upa => upa.Property)
                .Distinct()
                .OrderBy(p => p.Name)
                .ToListAsync();

            var currentPropertyId = (ViewBag.CurrentProperty as Property)?.Id;
            if (currentPropertyId.HasValue)
            {
                properties = properties
                    .Where(p => p.Id == currentPropertyId.Value)
                    .ToList();
            }

            return properties;
        }

        private async Task<PassOnLogFormViewModel> BuildFormViewModelAsync(PassOnLogFormViewModel model, List<Property> accessibleProperties)
        {
            model.SelectedPropertyIds ??= new List<int>();
            var selectedSet = new HashSet<int>(model.SelectedPropertyIds);

            model.PropertyOptions = accessibleProperties
                .Select(p => new PassOnLogPropertyOptionViewModel
                {
                    Id = p.Id,
                    Name = p.Name,
                    TranslatedName = p.Name,
                    Code = p.Code,
                    IsSelected = selectedSet.Contains(p.Id)
                })
                .ToList();

            var activeLanguage = HttpContext.Items["ActiveLanguage"] as string ?? _translationService.DefaultLanguage;
            var isDefaultLanguage = string.Equals(activeLanguage, _translationService.DefaultLanguage, StringComparison.OrdinalIgnoreCase);
            var cancellationToken = HttpContext.RequestAborted;

            if (!isDefaultLanguage && model.PropertyOptions.Count > 0)
            {
                foreach (var option in model.PropertyOptions)
                {
                    if (string.IsNullOrWhiteSpace(option.Name))
                    {
                        option.TranslatedName = option.Name;
                        continue;
                    }

                    var translatedName = await _translationService.TranslateDynamicAsync(
                        "Property",
                        option.Id.ToString(CultureInfo.InvariantCulture),
                        "Name",
                        option.Name,
                        _translationService.DefaultLanguage,
                        activeLanguage,
                        cancellationToken);

                    option.TranslatedName = string.IsNullOrWhiteSpace(translatedName)
                        ? option.Name
                        : translatedName;
                }
            }
            else
            {
                foreach (var option in model.PropertyOptions)
                {
                    option.TranslatedName = option.Name;
                }
            }

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

        private async Task<List<UploadedAttachmentInfo>> SaveAttachmentsAsync(IEnumerable<IFormFile>? files)
        {
            var uploads = new List<UploadedAttachmentInfo>();
            if (files == null)
            {
                return uploads;
            }

            var uploadRoot = Path.Combine(_environment.WebRootPath, "uploads", "passonlogs");
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

                var relativePath = Path.Combine("/uploads/passonlogs", uniqueName).Replace("\\", "/");

                uploads.Add(new UploadedAttachmentInfo
                {
                    Attachment = new PassOnLogAttachment
                    {
                        FilePath = relativePath,
                        OriginalFileName = originalFileName
                    },
                    PhysicalPath = physicalPath
                });
            }

            return uploads;
        }

        private string GetPhysicalPathForAttachment(string? storedPath)
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

        private void DeletePhysicalFile(string? physicalPath)
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
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete pass-on log attachment at {Path}", physicalPath);
            }
        }

        private static string GetAttachmentDisplayName(PassOnLogAttachment attachment)
        {
            if (!string.IsNullOrWhiteSpace(attachment.OriginalFileName))
            {
                return attachment.OriginalFileName;
            }

            if (string.IsNullOrWhiteSpace(attachment.FilePath))
            {
                return "Attachment";
            }

            return Path.GetFileName(attachment.FilePath);
        }

        private async Task<bool> MarkPassOnLogAlertsAsReadAsync(int logId, string userId)
        {
            var notifications = await _context.UserNotifications
                .Where(n => n.UserId == userId && n.Type == "passon-log" && !n.IsRead)
                .Where(n => n.PassOnLogId == logId || (n.PassOnLogId == null && n.LinkUrl != null))
                .ToListAsync();

            if (!notifications.Any())
            {
                return false;
            }

            var toRemove = new List<UserNotification>();

            foreach (var notification in notifications)
            {
                var matches = notification.PassOnLogId == logId;

                if (!matches && TryResolveLogIdFromLink(notification.LinkUrl, out var parsedId))
                {
                    matches = parsedId == logId;
                }

                if (!matches)
                {
                    continue;
                }

                toRemove.Add(notification);
            }

            if (!toRemove.Any())
            {
                return false;
            }

            _context.UserNotifications.RemoveRange(toRemove);
            return true;
        }

        private static bool TryResolveLogIdFromLink(string? linkUrl, out int logId)
        {
            logId = default;
            if (string.IsNullOrWhiteSpace(linkUrl))
            {
                return false;
            }

            var match = Regex.Match(linkUrl, @"PassOnLogs\/Details\/(?<id>\d+)", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return false;
            }

            return int.TryParse(match.Groups["id"].Value, out logId);
        }

        private async Task<PassOnLogDetailsViewModel> BuildDetailsViewModelAsync(PassOnLog log, string currentUserId, int? nextLogId, int? previousLogId, bool canDelete)
        {
            var creatorName = PassOnLogEmailHelper.FormatUserName(log.CreatedBy?.FirstName, log.CreatedBy?.LastName, log.CreatedBy?.Email ?? string.Empty);

            var vm = new PassOnLogDetailsViewModel
            {
                Id = log.Id,
                Title = log.Title,
                Body = log.Body,
                CreatorName = creatorName,
                CreatorAvatar = UserAvatarHelper.BuildFromUser(log.CreatedBy, creatorName, "xl"),
                CreatedAt = log.CreatedAt,
                UpdatedAt = log.UpdatedAt,
                PropertyNames = log.Properties.Select(lp => lp.Property.Name).Distinct().OrderBy(name => name).ToList(),
                Properties = log.Properties
                    .Where(lp => lp.Property != null)
                    .GroupBy(lp => lp.PropertyId)
                    .Select(group =>
                    {
                        var property = group.First().Property;
                        return new PassOnLogPropertyDisplayViewModel
                        {
                            Id = group.Key,
                            Name = property?.Name ?? string.Empty,
                            TranslatedName = property?.Name ?? string.Empty,
                            Code = property?.Code ?? string.Empty
                        };
                    })
                    .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                CanEdit = log.CreatedById == currentUserId,
                Comments = log.Comments
                    .OrderBy(c => c.CreatedAt)
                    .Select(c =>
                    {
                        var commentCreatorName = PassOnLogEmailHelper.FormatUserName(c.CreatedBy?.FirstName, c.CreatedBy?.LastName, c.CreatedBy?.Email ?? string.Empty);
                        return new PassOnLogCommentViewModel
                        {
                            Id = c.Id,
                            Body = c.Body,
                            TranslatedBody = c.Body,
                            CreatedAt = c.CreatedAt,
                            CreatorName = commentCreatorName,
                            CreatorAvatar = UserAvatarHelper.BuildFromUser(c.CreatedBy, commentCreatorName, "sm")
                        };
                    })
                    .ToList(),
                Viewers = log.Views
                    .OrderByDescending(v => v.ViewedAt)
                    .Select(v => new PassOnLogViewerViewModel
                    {
                        Name = PassOnLogEmailHelper.FormatUserName(v.Viewer?.FirstName, v.Viewer?.LastName, v.Viewer?.Email ?? string.Empty),
                        ViewedAt = v.ViewedAt
                    })
                    .ToList(),
                Attachments = log.Attachments
                    .OrderBy(a => GetAttachmentDisplayName(a), StringComparer.OrdinalIgnoreCase)
                    .Select(a => new PassOnLogAttachmentViewModel
                    {
                        Id = a.Id,
                        FileName = GetAttachmentDisplayName(a),
                        DownloadUrl = a.FilePath
                    })
                    .ToList(),
                NewComment = new PassOnLogCommentInputModel
                {
                    LogId = log.Id
                },
                NextLogId = nextLogId,
                PreviousLogId = previousLogId
            };

            vm.CanDelete = canDelete;
            vm.TranslatedTitle = vm.Title;
            vm.TranslatedBody = vm.Body;
            vm.TranslatedPropertyNames = vm.Properties
                .Select(property =>
                {
                    var displayName = string.IsNullOrWhiteSpace(property.TranslatedName) ? property.Name : property.TranslatedName;
                    if (string.IsNullOrWhiteSpace(displayName))
                    {
                        return null;
                    }

                    return string.IsNullOrWhiteSpace(property.Code)
                        ? displayName
                        : string.Format(CultureInfo.CurrentCulture, "{0} ({1})", displayName, property.Code);
                })
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList()!;

            var activeLanguage = HttpContext.Items["ActiveLanguage"] as string ?? _translationService.DefaultLanguage;
            var isDefaultLanguage = string.Equals(activeLanguage, _translationService.DefaultLanguage, StringComparison.OrdinalIgnoreCase);
            var cancellationToken = HttpContext.RequestAborted;

            if (!isDefaultLanguage)
            {
                var entityId = vm.Id.ToString(CultureInfo.InvariantCulture);

                if (!string.IsNullOrWhiteSpace(vm.Title))
                {
                    var translatedTitle = await _translationService.TranslateDynamicAsync(
                        "PassOnLog",
                        entityId,
                        "Title",
                        vm.Title,
                        _translationService.DefaultLanguage,
                        activeLanguage,
                        cancellationToken);
                    if (!string.IsNullOrWhiteSpace(translatedTitle))
                    {
                        vm.TranslatedTitle = translatedTitle;
                    }
                }

                if (!string.IsNullOrWhiteSpace(vm.Body))
                {
                    var translatedBody = await _translationService.TranslateDynamicAsync(
                        "PassOnLog",
                        entityId,
                        "Body",
                        vm.Body,
                        _translationService.DefaultLanguage,
                        activeLanguage,
                        cancellationToken);
                    if (!string.IsNullOrWhiteSpace(translatedBody))
                    {
                        vm.TranslatedBody = translatedBody;
                    }
                }

                if (vm.Properties is { Count: > 0 })
                {
                    foreach (var property in vm.Properties)
                    {
                        if (string.IsNullOrWhiteSpace(property.Name))
                        {
                            property.TranslatedName = property.Name;
                            continue;
                        }

                        var translatedPropertyName = await _translationService.TranslateDynamicAsync(
                            "Property",
                            property.Id.ToString(CultureInfo.InvariantCulture),
                            "Name",
                            property.Name,
                            _translationService.DefaultLanguage,
                            activeLanguage,
                            cancellationToken);

                        property.TranslatedName = string.IsNullOrWhiteSpace(translatedPropertyName)
                            ? property.Name
                            : translatedPropertyName;
                    }

                    vm.TranslatedPropertyNames = vm.Properties
                        .Select(property =>
                        {
                            var displayName = string.IsNullOrWhiteSpace(property.TranslatedName) ? property.Name : property.TranslatedName;
                            if (string.IsNullOrWhiteSpace(displayName))
                            {
                                return null;
                            }

                            return string.IsNullOrWhiteSpace(property.Code)
                                ? displayName
                                : string.Format(CultureInfo.CurrentCulture, "{0} ({1})", displayName, property.Code);
                        })
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .ToList()!;
                }

                foreach (var comment in vm.Comments)
                {
                    if (string.IsNullOrWhiteSpace(comment.Body))
                    {
                        comment.TranslatedBody = comment.Body;
                        continue;
                    }

                    var translatedComment = await _translationService.TranslateDynamicAsync(
                        "PassOnLogComment",
                        comment.Id.ToString(CultureInfo.InvariantCulture),
                        "Body",
                        comment.Body,
                        _translationService.DefaultLanguage,
                        activeLanguage,
                        cancellationToken);

                    comment.TranslatedBody = string.IsNullOrWhiteSpace(translatedComment)
                        ? comment.Body
                        : translatedComment;
                }
            }
            else
            {
                foreach (var property in vm.Properties)
                {
                    property.TranslatedName = property.Name;
                }

                foreach (var comment in vm.Comments)
                {
                    comment.TranslatedBody = comment.Body;
                }
            }

            return vm;
        }

        private async Task<(int? NextLogId, int? PreviousLogId)> GetNeighborLogIdsAsync(PassOnLog log, List<int> accessiblePropertyIds)
        {
            if (accessiblePropertyIds == null || accessiblePropertyIds.Count == 0)
            {
                return (null, null);
            }

            var baseQuery = _context.PassOnLogs
                .AsNoTracking()
                .Where(l => l.Properties.Any(p => accessiblePropertyIds.Contains(p.PropertyId)))
                .Where(l => l.Id != log.Id);

            var newerCandidate = await baseQuery
                .Where(l => l.CreatedAt > log.CreatedAt || (l.CreatedAt == log.CreatedAt && l.Id > log.Id))
                .OrderBy(l => l.CreatedAt)
                .ThenBy(l => l.Id)
                .Select(l => l.Id)
                .FirstOrDefaultAsync();

            var olderCandidate = await baseQuery
                .Where(l => l.CreatedAt < log.CreatedAt || (l.CreatedAt == log.CreatedAt && l.Id < log.Id))
                .OrderByDescending(l => l.CreatedAt)
                .ThenByDescending(l => l.Id)
                .Select(l => l.Id)
                .FirstOrDefaultAsync();

            int? nextLogId = newerCandidate == 0 ? null : newerCandidate;
            int? previousLogId = olderCandidate == 0 ? null : olderCandidate;

            return (nextLogId, previousLogId);
        }

        private sealed class UploadedAttachmentInfo
        {
            public PassOnLogAttachment Attachment { get; init; } = null!;
            public string PhysicalPath { get; init; } = string.Empty;
        }

        private void MarkLogUnreadForUnseenCommentViewers(PassOnLog log, string actorId, DateTime commentCreatedAt)
        {
            if (log.Views == null)
            {
                return;
            }

            var staleViews = log.Views
                .Where(v => v.ViewerId != actorId && v.ViewedAt < commentCreatedAt)
                .ToList();

            if (staleViews.Count > 0)
            {
                _context.PassOnLogViews.RemoveRange(staleViews);
                foreach (var view in staleViews)
                {
                    log.Views.Remove(view);
                }
            }

            var actorView = log.Views.FirstOrDefault(v => v.ViewerId == actorId);
            if (actorView == null)
            {
                var newView = new PassOnLogView
                {
                    PassOnLogId = log.Id,
                    ViewerId = actorId,
                    ViewedAt = commentCreatedAt
                };
                _context.PassOnLogViews.Add(newView);
                log.Views.Add(newView);
            }
            else
            {
                actorView.ViewedAt = commentCreatedAt;
            }
        }

        private static bool IsLogUnread(PassOnLog log, string userId)
        {
            if (log.CreatedById == userId)
            {
                return false;
            }

            return !log.Views.Any(v => v.ViewerId == userId);
        }

    }
}








