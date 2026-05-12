using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace hOps.web.Services.Localization
{
    public interface ILanguagePreferenceService
    {
        string GetDefaultLanguage();
        Task<string> GetPreferredLanguageAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default);
        Task SetPreferredLanguageAsync(ClaimsPrincipal user, string languageCode, CancellationToken cancellationToken = default);
        void SetPreferredLanguageCookie(string languageCode);
        string? GetPreferredLanguageFromCookie();
    }
}
