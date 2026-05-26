using System;
using System.Globalization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.DependencyInjection;
using hOps.web.Services.Localization;

namespace hOps.web.Localization
{
    public static class LocalizationViewExtensions
    {
        private const string ActiveLanguageItemKey = "ActiveLanguage";

        public static string GetActiveLanguage(this ViewContext viewContext)
        {
            if (viewContext?.HttpContext?.Items != null &&
                viewContext.HttpContext.Items.TryGetValue(ActiveLanguageItemKey, out var value) &&
                value is string language &&
                !string.IsNullOrWhiteSpace(language))
            {
                return language;
            }

            return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        }

        public static string GetActiveLanguage(this IHtmlHelper htmlHelper)
            => htmlHelper?.ViewContext?.GetActiveLanguage() ?? CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

        public static string Localize(this IHtmlHelper htmlHelper, string key, string? fallback = null)
        {
            if (htmlHelper == null)
            {
                return fallback ?? key;
            }

            var translationService = htmlHelper.ViewContext.HttpContext.RequestServices.GetRequiredService<ITranslationService>();
            var language = htmlHelper.GetActiveLanguage();
            return translationService.Translate(key, language, fallback ?? key);
        }
    }
}
