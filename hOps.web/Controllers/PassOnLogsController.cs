using hOps.web.Data;
using hOps.web.Models;
using hOps.web.Services;
using hOps.web.Utilities;
using hOps.web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Net;
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
        private readonly IEmailSender _emailSender;
        private readonly ILogger<PassOnLogsController> _logger;
        private readonly IWebHostEnvironment _environment;
        private readonly IRealtimeNotificationService _realtimeNotifications;

        public PassOnLogsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            MentionService mentionService,
            IEmailSender emailSender,
            ILogger<PassOnLogsController> logger,
            IWebHostEnvironment environment,
            IRealtimeNotificationService realtimeNotifications)
            : base(context, userManager)
        {
            _mentionService = mentionService;
            _emailSender = emailSender;
            _logger = logger;
            _environment = environment;
            _realtimeNotifications = realtimeNotifications;
        }

        public async Task<IActionResult> Index([FromQuery] PassOnLogFiltersViewModel? filters)
        {
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
                    Text = FormatUserName(creator.FirstName, creator.LastName, creator.Email ?? string.Empty),
                    Value = creator.Id,
                    Selected = creatorFilterSet.Contains(creator.Id)
                })
                .ToList();

            filters.PropertyOptions = accessibleProperties
                .Select(p => new PassOnLogPropertyOptionViewModel
                {
                    Id = p.Id,
                    Name = p.Name,
                    Code = p.Code,
                    IsSelected = filters.PropertyIds.Contains(p.Id)
                })
                .ToList();

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
                var creatorName = FormatUserName(log.CreatedBy?.FirstName, log.CreatedBy?.LastName, log.CreatedBy?.Email ?? string.Empty);

                return new PassOnLogListItemViewModel
                {
                    Id = log.Id,
                    Title = log.Title,
                    CreatorName = creatorName,
                    CreatorAvatar = UserAvatarHelper.BuildFromUser(log.CreatedBy, creatorName, "lg"),
                    CreatedAt = log.CreatedAt,
                    IsUnread = IsLogUnread(log, currentUser.Id),
                    PropertyNames = log.Properties.Select(lp => lp.Property.Name).Distinct().OrderBy(name => name).ToList(),
                    CommentCount = log.Comments.Count,
                    Preview = BuildPreview(log.Body)
                };
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

            var logAlertRecipients = await GetLogEntryAlertRecipientsAsync(log, currentUser);
            await NotifyLogSubscribersAsync(log, currentUser, link, logAlertRecipients);
            await SendLogEntryEmailsAsync(log, currentUser, link, logAlertRecipients);

            return RedirectToAction(nameof(Details), new { id = log.Id });
        }


        private async Task<List<ApplicationUser>> GetLogEntryAlertRecipientsAsync(PassOnLog log, ApplicationUser actor)
        {
            await _context.Entry(log)
                .Collection(l => l.Properties)
                .Query()
                .Include(lp => lp.Property)
                .LoadAsync();

            var propertyIds = log.Properties.Select(lp => lp.PropertyId).Distinct().ToList();

            var candidateUsers = await _context.Users
                .Where(u => !string.Equals(u.Id, actor.Id))
                .Select(u => new
                {
                    User = u,
                    PropertyPreferences = u.EmailPropertySubscriptions.Select(s => new { s.PropertyId, s.IncludeInLogAlerts }),
                    AccessIds = u.UserPropertyAccesses!.Select(upa => upa.PropertyId)
                })
                .ToListAsync();

            return candidateUsers
                .Where(candidate =>
                {
                    var allowedProperties = candidate.PropertyPreferences
                        .Where(p => p.IncludeInLogAlerts)
                        .Select(p => p.PropertyId)
                        .ToHashSet();

                    if (!allowedProperties.Any())
                    {
                        allowedProperties = candidate.AccessIds.ToHashSet();
                    }

                    if (!allowedProperties.Any())
                    {
                        return false;
                    }

                    if (!propertyIds.Any())
                    {
                        return true;
                    }

                    return propertyIds.Any(pid => allowedProperties.Contains(pid));
                })
                .Select(candidate => candidate.User)
                .ToList();
        }

        private async Task NotifyLogSubscribersAsync(
            PassOnLog log,
            ApplicationUser actor,
            string linkUrl,
            List<ApplicationUser> recipients)
        {
            if (!recipients.Any())
            {
                return;
            }

            var actorName = FormatUserName(actor.FirstName, actor.LastName, actor.Email ?? string.Empty);
            var now = DateTime.UtcNow;

            foreach (var recipient in recipients)
            {
                _context.UserNotifications.Add(new UserNotification
                {
                    UserId = recipient.Id,
                    Type = "passon-log",
                    Title = "New pass-on log",
                    Content = $"{actorName} posted \"{log.Title}\"",
                    LinkUrl = linkUrl,
                    PassOnLogId = log.Id,
                    CreatedAt = now,
                    IsRead = false
                });
            }

            await _context.SaveChangesAsync();

            var payload = new RealtimeNotificationPayload(
                "New pass-on log",
                $"{actorName} posted \"{log.Title}\"",
                linkUrl,
                "log");

            await _realtimeNotifications.NotifyUsersAsync(recipients.Select(r => r.Id), payload);
        }

        private async Task SendLogEntryEmailsAsync(
            PassOnLog log,
            ApplicationUser actor,
            string linkUrl,
            List<ApplicationUser> recipients)
        {
            var emailRecipients = recipients
                .Where(r => r.EmailOnLogEntry && !string.IsNullOrWhiteSpace(r.Email))
                .ToList();

            if (!emailRecipients.Any())
            {
                return;
            }

            var propertyNames = log.Properties
                .Select(lp => lp.Property?.Name ?? $"Property #{lp.PropertyId}")
                .Distinct()
                .ToList();

            var actorName = FormatUserName(actor.FirstName, actor.LastName, actor.Email ?? string.Empty);
            var subject = $"New log: {log.Title}";
            var preview = RichTextRenderer.ToPlainText(log.Body ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(preview) && preview.Length > 500)
            {
                preview = $"{preview[..500]}...";
            }

            var safeActor = WebUtility.HtmlEncode(actorName);
            var safeTitle = WebUtility.HtmlEncode(log.Title);
            var safePreview = string.IsNullOrWhiteSpace(preview)
                ? null
                : WebUtility.HtmlEncode(preview).Replace("\r\n", "\n").Replace("\n", "<br/>");
            var safeProperties = propertyNames.Any()
                ? string.Join(", ", propertyNames.Select(WebUtility.HtmlEncode))
                : null;

            var bodyBuilder = new StringBuilder();
            bodyBuilder.AppendLine($@"<p>{safeActor} posted a new log titled <strong>{safeTitle}</strong>.</p>");
            if (safeProperties != null)
            {
                bodyBuilder.AppendLine($@"<p><strong>Properties:</strong> {safeProperties}</p>");
            }
            if (safePreview != null)
            {
                bodyBuilder.AppendLine($@"<p><strong>Summary:</strong><br/>{safePreview}</p>");
            }
            bodyBuilder.AppendLine($@"<p><a href=""{linkUrl}"">Review the log</a></p>");

            var htmlBody = bodyBuilder.ToString();

            foreach (var recipient in emailRecipients)
            {
                try
                {
                    await _emailSender.SendEmailAsync(recipient.Email!, subject, htmlBody);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to send log email notification to user {UserId}", recipient.Id);
                }
            }
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

            model = BuildFormViewModel(model, accessibleProperties);

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

            var model = BuildDetailsViewModel(log, currentUser.Id, nextLogId, previousLogId);

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
                var modelWithErrors = BuildDetailsViewModel(log, currentUser.Id, nextLogId, previousLogId);
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

        private PassOnLogFormViewModel BuildFormViewModel(PassOnLogFormViewModel model, List<Property> accessibleProperties)
        {
            model.SelectedPropertyIds ??= new List<int>();
            var selectedSet = new HashSet<int>(model.SelectedPropertyIds);

            model.PropertyOptions = accessibleProperties
                .Select(p => new PassOnLogPropertyOptionViewModel
                {
                    Id = p.Id,
                    Name = p.Name,
                    Code = p.Code,
                    IsSelected = selectedSet.Contains(p.Id)
                })
                .ToList();

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

            var updated = false;
            var now = DateTime.UtcNow;

            foreach (var notification in notifications)
            {
                var matches = notification.PassOnLogId == logId;

                if (!matches && TryResolveLogIdFromLink(notification.LinkUrl, out var parsedId))
                {
                    notification.PassOnLogId = parsedId;
                    matches = parsedId == logId;
                }

                if (!matches)
                {
                    continue;
                }

                notification.IsRead = true;
                notification.ReadAt = now;
                updated = true;
            }

            return updated;
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

        private PassOnLogDetailsViewModel BuildDetailsViewModel(PassOnLog log, string currentUserId, int? nextLogId, int? previousLogId)
        {
            var creatorName = FormatUserName(log.CreatedBy?.FirstName, log.CreatedBy?.LastName, log.CreatedBy?.Email ?? string.Empty);

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
                CanEdit = log.CreatedById == currentUserId,
                Comments = log.Comments
                    .OrderBy(c => c.CreatedAt)
                    .Select(c =>
                    {
                        var commentCreatorName = FormatUserName(c.CreatedBy?.FirstName, c.CreatedBy?.LastName, c.CreatedBy?.Email ?? string.Empty);
                        return new PassOnLogCommentViewModel
                        {
                            Id = c.Id,
                            Body = c.Body,
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
                        Name = FormatUserName(v.Viewer?.FirstName, v.Viewer?.LastName, v.Viewer?.Email ?? string.Empty),
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








