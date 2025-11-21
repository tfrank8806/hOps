using Microsoft.Maui;
using Microsoft.Maui.Controls;

namespace hOps.Mobile.Helpers
{
    public static class ServiceHelper
    {
        public static T? GetService<T>() where T : class
        {
            return Current?.GetService(typeof(T)) as T;
        }

        private static IServiceProvider? Current =>
#if WINDOWS
            MauiWinUIApplication.Current.Services;
#else
            Application.Current?.Handler?.MauiContext?.Services ?? IPlatformApplication.Current?.Services;
#endif
    }
}
