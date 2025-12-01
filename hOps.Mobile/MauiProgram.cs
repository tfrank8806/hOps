using hOps.Mobile.Services;
using Microsoft.Extensions.Logging;

namespace hOps.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddSingleton(new ApiOptions());
        builder.Services.AddSingleton<ISecureTokenStore, SecureTokenStore>();
        builder.Services.AddSingleton<ICurrentUserStore, CurrentUserStore>();
        builder.Services.AddSingleton(new HttpClient());
        builder.Services.AddTransient<IApiClient, ApiClient>(sp =>
        {
            var options = sp.GetRequiredService<ApiOptions>();
            var http = sp.GetRequiredService<HttpClient>();
            http.BaseAddress = new Uri(options.BaseUrl);
            return new ApiClient(http, sp.GetRequiredService<ISecureTokenStore>());
        });
        builder.Services.AddTransient<IAuthService, AuthService>(sp =>
        {
            var options = sp.GetRequiredService<ApiOptions>();
            var http = sp.GetRequiredService<HttpClient>();
            http.BaseAddress = new Uri(options.BaseUrl);
            return new AuthService(http, sp.GetRequiredService<ISecureTokenStore>(), sp.GetRequiredService<ICurrentUserStore>());
        });
        builder.Services.AddTransient<ViewModels.LoginViewModel>();
        builder.Services.AddTransient<ViewModels.DashboardViewModel>();
        builder.Services.AddTransient<ViewModels.ProfileViewModel>();
        builder.Services.AddSingleton<IPassOnLogService, PassOnLogService>();
        builder.Services.AddTransient<Pages.LoginPage>();
        builder.Services.AddTransient<Pages.DashboardPage>();
        builder.Services.AddTransient<Pages.ProfilePage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
