using hOps.Mobile.Helpers;
using hOps.Mobile.ViewModels;

namespace hOps.Mobile.Pages;

public partial class DashboardPage : ContentPage
{
    public DashboardPage()
    {
        InitializeComponent();
        BindingContext = ServiceHelper.GetService<DashboardViewModel>()
            ?? throw new InvalidOperationException("DashboardViewModel not registered.");
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is DashboardViewModel vm)
        {
            await vm.LoadAsync();
        }
    }
}
