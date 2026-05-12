using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using hOps.web.Localization;
using hOps.web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace hOps.web.Services.Localization
{
    public sealed class LanguagePreferenceService : ILanguagePreferenceService
    {
        private const string LanguageCookieName = "hops.lang";

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<LanguagePreferenceService> _logger;

        public LanguagePreferenceService(
            UserManager<ApplicationUser> userManager,
            IHttpContextAccessor httpContextAccessor,
            ILogger<LanguagePreferenceService> logger)
        {
            _userManager = userManager;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public string GetDefaultLanguage() => LanguageConstants.English;

        public async Task<string> GetPreferredLanguageAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
        {
            var cookieLanguage = GetPreferredLanguageFromCookie();

            if (user?.Identity?.IsAuthenticated == true)
            {
                var appUser = await _userManager.GetUserAsync(user);
                if (appUser != null)
                {
                    if (!string.IsNullOrWhiteSpace(appUser.PreferredLanguage))
                    {
                        var normalized = NormalizeLanguage(appUser.PreferredLanguage);
                        if (!string.Equals(normalized, appUser.PreferredLanguage, StringComparison.OrdinalIgnoreCase))
                        {
                            await PersistUserLanguageAsync(appUser, normalized, cancellationToken);
                        }
                        return normalized;
                    }

                    if (!string.IsNullOrWhiteSpace(cookieLanguage))
                    {
                        var normalized = NormalizeLanguage(cookieLanguage);
                        await PersistUserLanguageAsync(appUser, normalized, cancellationToken);
                        return normalized;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(cookieLanguage))
            {
                return NormalizeLanguage(cookieLanguage);
            }

            return GetDefaultLanguage();
        }

        public async Task SetPreferredLanguageAsync(ClaimsPrincipal user, string languageCode, CancellationToken cancellationToken = default)
        {
            var normalizedLanguage = NormalizeLanguage(languageCode);
            SetPreferredLanguageCookie(normalizedLanguage);

            if (user?.Identity?.IsAuthenticated == true)
            {
                var appUser = await _userManager.GetUserAsync(user);
                if (appUser != null)
                {
                    await PersistUserLanguageAsync(appUser, normalizedLanguage, cancellationToken);
                }
            }
        }

        public void SetPreferredLanguageCookie(string languageCode)
        {
            var context = _httpContextAccessor.HttpContext;
            if (context?.Response == null)
            {
                return;
            }

            var normalized = NormalizeLanguage(languageCode);
            var cookieOptions = new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(2),
                HttpOnly = false,
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                Secure = context.Request?.IsHttps ?? false
            };

            context.Response.Cookies.Append(LanguageCookieName, normalized, cookieOptions);
            var requestCulture = new RequestCulture(normalized);
            context.Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(requestCulture),
                cookieOptions);
        }

        public string? GetPreferredLanguageFromCookie()
        {
            var context = _httpContextAccessor.HttpContext;
            if (context?.Request == null)
            {
                return null;
            }

            if (context.Request.Cookies.TryGetValue(LanguageCookieName, out var value))
            {
                return string.IsNullOrWhiteSpace(value) ? null : NormalizeLanguage(value);
            }

            return null;
        }

        private string NormalizeLanguage(string? languageCode)
        {
            if (string.IsNullOrWhiteSpace(languageCode))
            {
                return GetDefaultLanguage();
            }

            languageCode = languageCode.Trim().ToLowerInvariant();
            return LanguageConstants.SupportedLanguages.Any(l => string.Equals(l.Code, languageCode, StringComparison.OrdinalIgnoreCase))
                ? languageCode
                : GetDefaultLanguage();
        }

        private async Task PersistUserLanguageAsync(ApplicationUser appUser, string languageCode, CancellationToken cancellationToken)
        {
            if (string.Equals(appUser.PreferredLanguage, languageCode, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            appUser.PreferredLanguage = languageCode;
            try
            {
                await _userManager.UpdateAsync(appUser);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist preferred language {Language} for user {UserId}", languageCode, appUser.Id);
                if (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
            }
        }
    }
}
