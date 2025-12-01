using hOps.Mobile.Helpers;
using hOps.Mobile.ViewModels;

namespace hOps.Mobile.Pages;

public partial class ProfilePage : ContentPage
{
    public ProfilePage()
    {
        InitializeComponent();
        BindingContext = ServiceHelper.GetService<ProfileViewModel>()
            ?? throw new InvalidOperationException("ProfileViewModel not registered.");
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is ProfileViewModel vm)
        {
            await vm.LoadAsync();
        }
    }
}
