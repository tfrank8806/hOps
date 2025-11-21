using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using hOps.Mobile.Models;
using hOps.Mobile.Services;

namespace hOps.Mobile.ViewModels;

public sealed class DashboardViewModel : INotifyPropertyChanged
{
    private readonly IPassOnLogService _passOnLogService;
    private readonly IWorkOrderService _workOrderService;

    public DashboardViewModel(IPassOnLogService passOnLogService, IWorkOrderService workOrderService)
    {
        _passOnLogService = passOnLogService;
        _workOrderService = workOrderService;
    }

    public ObservableCollection<PassOnLogListItem> Logs { get; } = new();
    public ObservableCollection<WorkOrderListItem> WorkOrders { get; } = new();

    public async Task LoadAsync()
    {
        var logs = await _passOnLogService.GetRecentLogsAsync();
        UpdateCollection(Logs, logs);

        var workOrders = await _workOrderService.GetRecentAsync();
        UpdateCollection(WorkOrders, workOrders);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static void UpdateCollection<T>(ObservableCollection<T> collection, IEnumerable<T> items)
    {
        collection.Clear();
        foreach (var item in items)
        {
            collection.Add(item);
        }
    }
}
