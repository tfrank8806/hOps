using hOps.Mobile.Helpers;
using hOps.Mobile.ViewModels;

namespace hOps.Mobile.Pages;

public partial class LoginPage : ContentPage
{
    public LoginPage()
    {
        InitializeComponent();
        BindingContext = ServiceHelper.GetService<LoginViewModel>() ?? throw new InvalidOperationException("LoginViewModel not registered.");
    }
}
