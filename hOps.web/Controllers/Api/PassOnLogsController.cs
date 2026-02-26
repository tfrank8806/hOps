using hOps.web.Data;
using hOps.web.Models;
using hOps.web.Services;
using hOps.web.ViewModels.Api;
using hOps.web.ViewModels;
using hOps.web.Utilities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hOps.web.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class PassOnLogsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IPropertyAccessService _propertyAccessService;
        private readonly MentionService _mentionService;

        public PassOnLogsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IPropertyAccessService propertyAccessService,
            MentionService mentionService)
        {
            _context = context;
            _userManager = userManager;
            _propertyAccessService = propertyAccessService;
            _mentionService = mentionService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<PassOnLogListItemDto>>> GetLogs([FromQuery] int? propertyId = null, [FromQuery] int take = 25)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Unauthorized();
            }

            var allowedPropertyIds = await _propertyAccessService.GetPropertyIdsForUserAsync(currentUser.Id);
            if (!allowedPropertyIds.Any())
            {
                return Forbid();
            }

            if (propertyId.HasValue && !allowedPropertyIds.Contains(propertyId.Value))
            {
                return Forbid();
            }

            var takeCount = Math.Clamp(take, 5, 100);

            var query = _context.PassOnLogs
                .AsNoTracking()
                .AsSplitQuery()
                .Include(l => l.CreatedBy)
                .Include(l => l.Properties).ThenInclude(lp => lp.Property)
                .Include(l => l.Views)
                .Include(l => l.Comments)
                .Where(l => l.Properties.Any(p => allowedPropertyIds.Contains(p.PropertyId)));

            if (propertyId.HasValue)
            {
                query = query.Where(l => l.Properties.Any(p => p.PropertyId == propertyId.Value));
            }

            var logs = await query
                .OrderByDescending(l => l.CreatedAt)
                .Take(takeCount)
                .ToListAsync();

            var items = logs.Select(log => MapListItem(log, currentUser.Id)).ToList();
            return Ok(items);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<PassOnLogDetailDto>> GetLog(int id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Unauthorized();
            }

            var allowedPropertyIds = await _propertyAccessService.GetPropertyIdsForUserAsync(currentUser.Id);
            if (!allowedPropertyIds.Any())
            {
                return Forbid();
            }

            var log = await _context.PassOnLogs
                .AsNoTracking()
                .AsSplitQuery()
                .Include(l => l.CreatedBy)
                .Include(l => l.Properties).ThenInclude(lp => lp.Property)
                .Include(l => l.Attachments)
                .Include(l => l.Views)
                .Include(l => l.Comments).ThenInclude(c => c.CreatedBy)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (log == null || !log.Properties.Any(p => allowedPropertyIds.Contains(p.PropertyId)))
            {
                return NotFound();
            }

            var dto = MapDetail(log);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<PassOnLogDetailDto>> CreateLog([FromBody] CreatePassOnLogRequest request)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Unauthorized();
            }

            var allowedPropertyIds = await _propertyAccessService.GetPropertyIdsForUserAsync(currentUser.Id);
            if (!allowedPropertyIds.Any())
            {
                return Forbid();
            }

            var selectedPropertyIds = request.PropertyIds
                .Where(id => allowedPropertyIds.Contains(id))
                .Distinct()
                .ToList();

            if (!selectedPropertyIds.Any())
            {
                return BadRequest(new { error = "At least one accessible property is required." });
            }

            var log = new PassOnLog
            {
                Title = request.Title.Trim(),
                Body = request.Body.Trim(),
                CreatedAt = DateTime.UtcNow,
                CreatedById = currentUser.Id
            };

            foreach (var propertyId in selectedPropertyIds)
            {
                log.Properties.Add(new PassOnLogProperty
                {
                    PropertyId = propertyId
                });
            }

            _context.PassOnLogs.Add(log);
            await _context.SaveChangesAsync();

            var link = Url.Action("Details", "PassOnLogs", new { id = log.Id }, Request.Scheme)
                ?? Url.Action("Index", "PassOnLogs") ?? "/PassOnLogs";

            if (!string.IsNullOrWhiteSpace(log.Body))
            {
                await _mentionService.CreateMentionNotificationsAsync(
                    log.Body,
                    currentUser,
                    $"Pass On Log: {log.Title}",
                    link,
                    log.Body);
            }

            var savedLog = await _context.PassOnLogs
                .AsSplitQuery()
                .Include(l => l.CreatedBy)
                .Include(l => l.Properties).ThenInclude(lp => lp.Property)
                .Include(l => l.Attachments)
                .Include(l => l.Comments).ThenInclude(c => c.CreatedBy)
                .FirstAsync(l => l.Id == log.Id);

            var dto = MapDetail(savedLog);
            return CreatedAtAction(nameof(GetLog), new { id = dto.Id }, dto);
        }

        [HttpPost("{id:int}/comments")]
        public async Task<ActionResult<PassOnLogCommentDto>> AddComment(int id, [FromBody] AddPassOnLogCommentRequest request)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Unauthorized();
            }

            var allowedPropertyIds = await _propertyAccessService.GetPropertyIdsForUserAsync(currentUser.Id);
            if (!allowedPropertyIds.Any())
            {
                return Forbid();
            }

            var log = await _context.PassOnLogs
                .Include(l => l.Properties)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (log == null || !log.Properties.Any(p => allowedPropertyIds.Contains(p.PropertyId)))
            {
                return NotFound();
            }

            var trimmedBody = request.Body.Trim();
            if (string.IsNullOrWhiteSpace(trimmedBody))
            {
                return BadRequest(new { error = "Comment body is required." });
            }

            var comment = new PassOnLogComment
            {
                PassOnLogId = log.Id,
                Body = trimmedBody,
                CreatedAt = DateTime.UtcNow,
                CreatedById = currentUser.Id
            };

            _context.PassOnLogComments.Add(comment);
            await _context.SaveChangesAsync();

            var link = Url.Action("Details", "PassOnLogs", new { id = log.Id }, Request.Scheme)
                ?? Url.Action("Index", "PassOnLogs") ?? "/PassOnLogs";

            await _mentionService.CreateMentionNotificationsAsync(
                comment.Body,
                currentUser,
                $"Pass On Log Comment: {log.Title}",
                link,
                comment.Body);

            await _context.Entry(comment).Reference(c => c.CreatedBy).LoadAsync();

            var dto = new PassOnLogCommentDto
            {
                Id = comment.Id,
                Body = comment.Body,
                CreatorName = PassOnLogEmailHelper.FormatUserName(comment.CreatedBy),
                CreatorPhotoUrl = comment.CreatedBy?.ProfilePhotoPath,
                CreatedAtUtc = comment.CreatedAt
            };

            return Ok(dto);
        }

        private static PassOnLogListItemDto MapListItem(PassOnLog log, string currentUserId)
        {
            return new PassOnLogListItemDto
            {
                Id = log.Id,
                Title = log.Title,
                Preview = BuildPreview(log.Body),
                CreatorName = PassOnLogEmailHelper.FormatUserName(log.CreatedBy),
                CreatorPhotoUrl = log.CreatedBy?.ProfilePhotoPath,
                CreatedAtUtc = log.CreatedAt,
                IsUnread = IsLogUnread(log, currentUserId),
                Properties = log.Properties
                    .Select(lp => lp.Property != null
                        ? (string.IsNullOrWhiteSpace(lp.Property.Code)
                            ? lp.Property.Name
                            : $"{lp.Property.Name} ({lp.Property.Code})")
                        : $"Property #{lp.PropertyId}")
                    .Distinct()
                    .ToList(),
                CommentCount = log.Comments.Count
            };
        }

        private static PassOnLogDetailDto MapDetail(PassOnLog log)
        {
            return new PassOnLogDetailDto
            {
                Id = log.Id,
                Title = log.Title,
                Body = log.Body,
                CreatorName = PassOnLogEmailHelper.FormatUserName(log.CreatedBy),
                CreatorPhotoUrl = log.CreatedBy?.ProfilePhotoPath,
                CreatedAtUtc = log.CreatedAt,
                UpdatedAtUtc = log.UpdatedAt,
                Properties = log.Properties
                    .Select(lp => lp.Property != null
                        ? (string.IsNullOrWhiteSpace(lp.Property.Code)
                            ? lp.Property.Name
                            : $"{lp.Property.Name} ({lp.Property.Code})")
                        : $"Property #{lp.PropertyId}")
                    .Distinct()
                    .ToList(),
                Comments = log.Comments
                    .OrderBy(c => c.CreatedAt)
                    .Select(c => new PassOnLogCommentDto
                    {
                        Id = c.Id,
                        Body = c.Body,
                        CreatorName = PassOnLogEmailHelper.FormatUserName(c.CreatedBy),
                        CreatorPhotoUrl = c.CreatedBy?.ProfilePhotoPath,
                        CreatedAtUtc = c.CreatedAt
                    })
                    .ToList(),
                Attachments = log.Attachments
                    .Select(a => new PassOnLogAttachmentDto
                    {
                        Id = a.Id,
                        FileName = string.IsNullOrWhiteSpace(a.OriginalFileName) ? a.FilePath : a.OriginalFileName,
                        DownloadUrl = a.FilePath
                    })
                    .ToList()
            };
        }

        private static bool IsLogUnread(PassOnLog log, string userId)
        {
            if (log.CreatedById == userId)
            {
                return false;
            }

            return !log.Views.Any(v => v.ViewerId == userId);
        }

        private static string BuildPreview(string? body)
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
                : $"{preview[..180]}...";
        }

    }
}
