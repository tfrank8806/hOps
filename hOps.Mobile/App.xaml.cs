using hOps.Mobile.Services;

namespace hOps.Mobile;

public partial class App : Application
{
    private readonly IAuthService _authService;

	public App(IAuthService authService)
	{
		InitializeComponent();
        _authService = authService;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
        var shell = new AppShell();
        shell.Dispatcher.Dispatch(async () =>
        {
            var hasToken = await _authService.HasTokenAsync();
            await Shell.Current.GoToAsync(hasToken ? "//DashboardPage" : "//LoginPage");
        });
		return new Window(shell);
	}
}
