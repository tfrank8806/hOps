using System;
using System.Linq;
using System.Threading.Tasks;
using hOps.web.Data;
using hOps.web.Models;
using hOps.web.Services;
using hOps.web.ViewModels.MailLog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace hOps.web.Controllers
{
    [Authorize]
    public class MailLogController : BaseController
    {
        private readonly ILogger<MailLogController> _logger;
        private readonly MentionService _mentionService;
        private readonly IUserTimeZoneService _timeZoneService;

        public MailLogController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<MailLogController> logger,
            MentionService mentionService,
            IUserTimeZoneService timeZoneService)
            : base(context, userManager)
        {
            _logger = logger;
            _mentionService = mentionService;
            _timeZoneService = timeZoneService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            ViewData["Title"] = "Package & Mail Log";
            var viewModel = await BuildIndexViewModelAsync(null);
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PackageLogEntryForm form)
        {
            ViewData["Title"] = "Package & Mail Log";

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var currentProperty = ViewBag.CurrentProperty as Property;
            if (currentProperty == null)
            {
                ModelState.AddModelError(string.Empty, "Select a property before adding package log entries.");
            }

            if (!ModelState.IsValid)
            {
                var invalidModel = await BuildIndexViewModelAsync(form);
                return View("Index", invalidModel);
            }

            var entry = new PackageLogEntry
            {
                PropertyId = currentProperty!.Id,
                RecipientName = form.RecipientName.Trim(),
                RoomNumber = form.RoomNumber?.Trim(),
                Carrier = form.Carrier?.Trim(),
                TrackingNumber = form.TrackingNumber?.Trim(),
                StorageLocation = form.StorageLocation?.Trim(),
                ArrivalDate = NormalizeDate(form.ArrivalDate),
                DepartureDate = NormalizeDate(form.DepartureDate),
                Notes = form.Notes?.Trim(),
                LoggedAt = DateTime.UtcNow,
                LoggedById = user.Id,
                Delivered = false,
                DeliveredAt = null
            };

            _context.PackageLogEntries.Add(entry);
            await _context.SaveChangesAsync();

            var link = Url.Action(nameof(Index), "MailLog", null, Request.Scheme) ?? "/MailLog";

            await _mentionService.CreateMentionNotificationsAsync(
                entry.Notes,
                user,
                $"Mail Log Entry for {entry.RecipientName}",
                link,
                entry.Notes);

            TempData["MailLogMessage"] = "Package entry added.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleDelivered(int id, bool delivered)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var entry = await _context.PackageLogEntries.FirstOrDefaultAsync(e => e.Id == id);
            if (entry == null)
            {
                return NotFound();
            }

            if (!await UserHasAccessToPropertyAsync(entry.PropertyId, user))
            {
                return Forbid();
            }

            entry.Delivered = delivered;
            entry.DeliveredAt = delivered ? DateTime.UtcNow : null;

            await _context.SaveChangesAsync();

            var message = delivered
                ? "Package marked as delivered."
                : "Package marked as awaiting pickup.";

            if (IsAjaxRequest())
            {
                var deliveredLocal = entry.DeliveredAt.HasValue
                    ? _timeZoneService.ConvertToUserTime(entry.DeliveredAt.Value).ToString("g")
                    : null;

                return Json(new
                {
                    success = true,
                    delivered = entry.Delivered,
                    deliveredAt = deliveredLocal,
                    message
                });
            }

            TempData["MailLogMessage"] = message;
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var entry = await _context.PackageLogEntries.FirstOrDefaultAsync(e => e.Id == id);
            if (entry == null)
            {
                return NotFound();
            }

            if (!await UserHasAccessToPropertyAsync(entry.PropertyId, user))
            {
                return Forbid();
            }

            _context.PackageLogEntries.Remove(entry);
            await _context.SaveChangesAsync();

            TempData["MailLogMessage"] = "Package entry removed.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<MailLogIndexViewModel> BuildIndexViewModelAsync(PackageLogEntryForm? form)
        {
            var viewModel = new MailLogIndexViewModel
            {
                Form = form ?? new PackageLogEntryForm()
            };

            var currentProperty = ViewBag.CurrentProperty as Property;
            if (currentProperty == null)
            {
                return viewModel;
            }

            viewModel.CurrentPropertyId = currentProperty.Id;
            viewModel.CurrentPropertyName = currentProperty.Name;

            var entries = await _context.PackageLogEntries
                .Where(e => e.PropertyId == currentProperty.Id)
                .OrderByDescending(e => e.LoggedAt)
                .Include(e => e.LoggedBy)
                .AsNoTracking()
                .ToListAsync();

            viewModel.Entries = entries
                .Select(e => new PackageLogEntryRowViewModel
                {
                    Id = e.Id,
                    RecipientName = e.RecipientName,
                    RoomNumber = e.RoomNumber,
                    Carrier = e.Carrier,
                    TrackingNumber = e.TrackingNumber,
                    StorageLocation = e.StorageLocation,
                    ArrivalDate = e.ArrivalDate,
                    DepartureDate = e.DepartureDate,
                    Notes = e.Notes,
                    LoggedAt = e.LoggedAt,
                    LoggedByName = BuildDisplayName(e.LoggedBy),
                    Delivered = e.Delivered,
                    DeliveredAt = e.DeliveredAt
                })
                .ToList();

            return viewModel;
        }

        private static DateTime? NormalizeDate(DateTime? date)
        {
            if (!date.HasValue)
            {
                return null;
            }

            var value = date.Value.Date;
            return DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        private async Task<bool> UserHasAccessToPropertyAsync(int propertyId, ApplicationUser user)
        {
            return await _context.UserPropertyAccesses
                .AnyAsync(upa => upa.PropertyId == propertyId && upa.ApplicationUserId == user.Id);
        }

        private bool IsAjaxRequest()
        {
            if (Request.Headers.TryGetValue("X-Requested-With", out var headerValue) &&
                headerValue == "XMLHttpRequest")
            {
                return true;
            }

            var acceptHeader = Request.Headers["Accept"].ToString();
            return !string.IsNullOrWhiteSpace(acceptHeader) &&
                   acceptHeader.Contains("application/json", StringComparison.OrdinalIgnoreCase);
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

            return string.IsNullOrWhiteSpace(user.Email)
                ? user.UserName ?? string.Empty
                : user.Email;
        }
    }
}
