using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using hOps.Mobile.Models;
using hOps.Mobile.Services;
using System.Windows.Input;

namespace hOps.Mobile.ViewModels;

public sealed class DashboardViewModel : INotifyPropertyChanged
{
    private readonly IPassOnLogService _passOnLogService;
    private readonly IWorkOrderService _workOrderService;
    private readonly IAuthService _authService;

    public DashboardViewModel(IPassOnLogService passOnLogService, IWorkOrderService workOrderService, IAuthService authService)
    {
        _passOnLogService = passOnLogService;
        _workOrderService = workOrderService;
        _authService = authService;
        LogoutCommand = new Command(async () => await LogoutAsync());
    }

    public ObservableCollection<PassOnLogListItem> Logs { get; } = new();
    public ObservableCollection<WorkOrderListItem> WorkOrders { get; } = new();
    public ICommand LogoutCommand { get; }

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

    private async Task LogoutAsync()
    {
        await _authService.LogoutAsync();
        await Shell.Current.GoToAsync("//LoginPage");
    }
}
