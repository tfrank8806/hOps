using System.Linq;
using System.Threading.Tasks;
using hOps.web.Data;
using hOps.web.Models;
using hOps.web.Services;
using hOps.web.ViewModels.Messages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace hOps.web.Controllers
{
    [Authorize]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class NotificationsController : BaseController
    {
        private readonly DirectMessageService _messageService;
        private readonly IUserTimeZoneService _timeZoneService;

        public NotificationsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            DirectMessageService messageService,
            IUserTimeZoneService timeZoneService)
            : base(context, userManager)
        {
            _messageService = messageService;
            _timeZoneService = timeZoneService;
        }

        [HttpGet]
        public async Task<IActionResult> Summary()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Unauthorized();
            }

            var notifications = await _messageService.GetRecentNotificationsAsync(currentUser.Id);
            var items = notifications
                .Select(n => new NotificationListItemViewModel
                {
                    Id = n.Id,
                    Title = n.Title,
                    Content = n.Content,
                    LinkUrl = n.LinkUrl,
                    CreatedAt = _timeZoneService.ConvertToUserTime(n.CreatedAt),
                    IsRead = n.IsRead,
                    Type = n.Type
                })
                .ToList();

            var unreadCount = notifications.Count(n => !n.IsRead);

            return Json(new
            {
                unreadCount,
                items
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Unauthorized();
            }

            await _messageService.MarkNotificationAsReadAsync(id, currentUser.Id);
            return RedirectToAction("Index", "DirectMessages");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllRead()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Unauthorized();
            }

            await _messageService.MarkAllNotificationsReadAsync(currentUser.Id);
            return RedirectToAction("Index", "DirectMessages");
        }
    }
}
