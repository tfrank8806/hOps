using hOps.Mobile.Models;

namespace hOps.Mobile.Services
{
    public interface IWorkOrderService
    {
        Task<IReadOnlyList<WorkOrderListItem>> GetRecentAsync(int take = 25, CancellationToken cancellationToken = default);
    }

    internal sealed class WorkOrderService : IWorkOrderService
    {
        private readonly IApiClient _apiClient;

        public WorkOrderService(IApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        public async Task<IReadOnlyList<WorkOrderListItem>> GetRecentAsync(int take = 25, CancellationToken cancellationToken = default)
        {
            var response = await _apiClient.GetAsync<List<WorkOrderListItem>>($"api/workorders?take={take}", cancellationToken);
            return response ?? new List<WorkOrderListItem>();
        }
    }
}
