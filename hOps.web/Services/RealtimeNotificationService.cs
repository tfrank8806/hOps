using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using hOps.web.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace hOps.web.Services
{
    public interface IRealtimeNotificationService
    {
        Task NotifyUserAsync(string userId, RealtimeNotificationPayload payload);
        Task NotifyUsersAsync(IEnumerable<string> userIds, RealtimeNotificationPayload payload);
    }

    public record RealtimeNotificationPayload(string Title, string Message, string Url, string Type);

    public class RealtimeNotificationService : IRealtimeNotificationService
    {
        private readonly IHubContext<NotificationHub> _hubContext;

        public RealtimeNotificationService(IHubContext<NotificationHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public Task NotifyUserAsync(string userId, RealtimeNotificationPayload payload)
        {
            return string.IsNullOrWhiteSpace(userId)
                ? Task.CompletedTask
                : _hubContext.Clients.Group(NotificationHub.BuildGroupName(userId)).SendAsync("ReceiveNotification", payload);
        }

        public Task NotifyUsersAsync(IEnumerable<string> userIds, RealtimeNotificationPayload payload)
        {
            var targets = userIds?
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(NotificationHub.BuildGroupName)
                .ToList() ?? new List<string>();

            if (!targets.Any())
            {
                return Task.CompletedTask;
            }

            return _hubContext.Clients.Groups(targets).SendAsync("ReceiveNotification", payload);
        }
    }
}
