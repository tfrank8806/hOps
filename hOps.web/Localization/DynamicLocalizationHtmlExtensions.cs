using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using hOps.web.Services.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace hOps.web.Localization
{
    public static class DynamicLocalizationHtmlExtensions
    {
        private const string DynamicTranslationCacheKey = "__DynamicTranslationCache";

        public static Task<string> DisplayTranslatedTextAsync(
            this IHtmlHelper htmlHelper,
            string entityType,
            string entityId,
            string field,
            string? sourceText,
            string? sourceLanguage = null,
            CancellationToken cancellationToken = default)
        {
            return TranslateDynamicTextAsync(htmlHelper, entityType, entityId, field, sourceText, sourceLanguage, cancellationToken);
        }

        public static async Task<string> TranslateDynamicTextAsync(
            this IHtmlHelper htmlHelper,
            string entityType,
            string entityId,
            string field,
            string? sourceText,
            string? sourceLanguage = null,
            CancellationToken cancellationToken = default)
        {
            if (htmlHelper == null)
            {
                throw new ArgumentNullException(nameof(htmlHelper));
            }

            var viewContext = htmlHelper.ViewContext ?? throw new InvalidOperationException("ViewContext is required for translation.");
            var httpContext = viewContext.HttpContext ?? throw new InvalidOperationException("HttpContext is required for translation.");
            var text = sourceText ?? string.Empty;

            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var translationService = httpContext.RequestServices.GetRequiredService<ITranslationService>();
            var targetLanguage = htmlHelper.GetActiveLanguage();
            if (string.Equals(targetLanguage, translationService.DefaultLanguage, StringComparison.OrdinalIgnoreCase))
            {
                return text;
            }

            var cache = GetOrCreateCache(viewContext);
            var cacheKey = BuildCacheKey(entityType, entityId, field, targetLanguage, text);
            if (cache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            var translated = await translationService.TranslateDynamicAsync(
                entityType,
                entityId,
                field,
                text,
                sourceLanguage ?? translationService.DefaultLanguage,
                targetLanguage,
                cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(translated))
            {
                translated = text;
            }

            cache[cacheKey] = translated;
            return translated;
        }

        private static IDictionary<string, string> GetOrCreateCache(ViewContext viewContext)
        {
            if (viewContext.HttpContext.Items.TryGetValue(DynamicTranslationCacheKey, out var cached) &&
                cached is IDictionary<string, string> dictionary)
            {
                return dictionary;
            }

            var cache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            viewContext.HttpContext.Items[DynamicTranslationCacheKey] = cache;
            return cache;
        }

        private static string BuildCacheKey(string entityType, string entityId, string field, string targetLanguage, string sourceText)
        {
            var typeKey = string.IsNullOrWhiteSpace(entityType) ? "General" : entityType.Trim();
            var idKey = string.IsNullOrWhiteSpace(entityId) ? "0" : entityId.Trim();
            var fieldKey = string.IsNullOrWhiteSpace(field) ? "General" : field.Trim();
            var compositeHash = HashCode.Combine(typeKey, idKey, fieldKey, targetLanguage, sourceText);
            return $"{typeKey}|{idKey}|{fieldKey}|{targetLanguage}|{compositeHash}";
        }
    }
}
