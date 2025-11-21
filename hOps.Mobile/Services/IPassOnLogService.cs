using hOps.Mobile.Models;

namespace hOps.Mobile.Services
{
    public interface IPassOnLogService
    {
        Task<IReadOnlyList<PassOnLogListItem>> GetRecentLogsAsync(int take = 25, CancellationToken cancellationToken = default);
    }

    internal sealed class PassOnLogService : IPassOnLogService
    {
        private readonly IApiClient _apiClient;

        public PassOnLogService(IApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        public async Task<IReadOnlyList<PassOnLogListItem>> GetRecentLogsAsync(int take = 25, CancellationToken cancellationToken = default)
        {
            var response = await _apiClient.GetAsync<List<PassOnLogListItem>>($"api/passonlogs?take={take}", cancellationToken);
            return response ?? new List<PassOnLogListItem>();
        }
    }
}
