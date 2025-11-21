using System.Text.Json;
using hOps.Mobile.Services;

namespace hOps.Mobile.Services
{
    public interface ICurrentUserStore
    {
        Task SetUserAsync(UserSummaryDto user);
        Task<UserSummaryDto?> GetUserAsync();
        Task ClearAsync();
    }

    internal sealed class CurrentUserStore : ICurrentUserStore
    {
        private const string UserKey = "current_user";
        private readonly IDictionary<string, string> _memoryStore = new Dictionary<string, string>();

        public Task SetUserAsync(UserSummaryDto user)
        {
            var json = JsonSerializer.Serialize(user);
            _memoryStore[UserKey] = json;
            return Task.CompletedTask;
        }

        public Task<UserSummaryDto?> GetUserAsync()
        {
            if (_memoryStore.TryGetValue(UserKey, out var json))
            {
                return Task.FromResult(JsonSerializer.Deserialize<UserSummaryDto>(json));
            }

            return Task.FromResult<UserSummaryDto?>(null);
        }

        public Task ClearAsync()
        {
            _memoryStore.Remove(UserKey);
            return Task.CompletedTask;
        }
    }
}
