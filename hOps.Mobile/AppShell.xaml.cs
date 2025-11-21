namespace hOps.Mobile;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute(nameof(Pages.LoginPage), typeof(Pages.LoginPage));
        Routing.RegisterRoute(nameof(Pages.DashboardPage), typeof(Pages.DashboardPage));
    }
}
