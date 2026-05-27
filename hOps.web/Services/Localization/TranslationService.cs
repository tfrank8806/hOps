using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using hOps.web.Data;
using hOps.web.Localization;
using hOps.web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace hOps.web.Services.Localization
{
    public sealed class TranslationService : ITranslationService
    {
        private readonly ApplicationDbContext _context;
        private readonly StaticTranslationStore _staticStore;
        private readonly IExternalTranslationProvider _translationProvider;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<TranslationService> _logger;

        private const string PlaceholderFormat = "__HOPS_TOKEN_{0}__";
        private static readonly Regex CurlyTokenRegex = new(@"\{\{.*?\}\}", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.CultureInvariant);
        private static readonly Regex HtmlTagRegex = new(@"<[^>]+>", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex UrlRegex = new(@"https?://[^\s]+", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        public TranslationService(
            ApplicationDbContext context,
            StaticTranslationStore staticStore,
            IExternalTranslationProvider translationProvider,
            IMemoryCache memoryCache,
            ILogger<TranslationService> logger)
        {
            _context = context;
            _staticStore = staticStore;
            _translationProvider = translationProvider;
            _memoryCache = memoryCache;
            _logger = logger;
        }

        public string DefaultLanguage => LanguageConstants.English;

        public IReadOnlyList<LanguageOption> SupportedLanguages => LanguageConstants.SupportedLanguages;

        public string Translate(string key, string targetLanguage, string? fallback = null)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return fallback ?? string.Empty;
            }

            if (string.Equals(targetLanguage, DefaultLanguage, StringComparison.OrdinalIgnoreCase))
            {
                return fallback ?? key;
            }

            return TryTranslate(key, targetLanguage, out var translated)
                ? translated
                : (fallback ?? key);
        }

        public bool TryTranslate(string key, string targetLanguage, out string translation)
        {
            translation = key;

            if (string.IsNullOrWhiteSpace(key))
            {
                translation = string.Empty;
                return false;
            }

            var normalized = NormalizeLanguage(targetLanguage);
            if (string.Equals(normalized, DefaultLanguage, StringComparison.OrdinalIgnoreCase))
            {
                translation = key;
                return true;
            }

            var dictionary = _staticStore.GetTranslations(normalized);
            if (dictionary.TryGetValue(key, out var translated) && !string.IsNullOrWhiteSpace(translated))
            {
                translation = translated;
                return true;
            }

            translation = key;
            return false;
        }

        public async Task<string> TranslateDynamicAsync(
            string entityType,
            string entityId,
            string field,
            string sourceText,
            string sourceLanguage,
            string targetLanguage,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sourceText))
            {
                return string.Empty;
            }

            var normalizedSource = NormalizeLanguage(sourceLanguage);
            var normalizedTarget = NormalizeLanguage(targetLanguage);

            if (string.Equals(normalizedSource, normalizedTarget, StringComparison.OrdinalIgnoreCase))
            {
                return sourceText;
            }

            var normalizedEntityType = string.IsNullOrWhiteSpace(entityType) ? "General" : entityType.Trim();
            var normalizedEntityId = string.IsNullOrWhiteSpace(entityId) ? "0" : entityId.Trim();
            var normalizedField = string.IsNullOrWhiteSpace(field) ? "General" : field.Trim();
            var hash = ComputeHash(sourceText);
            var cacheKey = BuildCacheKey(normalizedEntityType, normalizedEntityId, normalizedField, normalizedSource, normalizedTarget, hash);
            var sanitizedSource = ProtectTranslationTokens(sourceText, out var placeholderMap);

            if (_memoryCache.TryGetValue(cacheKey, out string? cachedValue))
            {
                return cachedValue ?? string.Empty;
            }

            try
            {
                var existing = await _context.TranslatedTexts
                    .FirstOrDefaultAsync(
                        t => t.EntityType == normalizedEntityType &&
                             t.EntityId == normalizedEntityId &&
                             t.Field == normalizedField &&
                             t.TargetLanguage == normalizedTarget,
                        cancellationToken);

                if (existing != null && string.Equals(existing.SourceTextHash, hash, StringComparison.OrdinalIgnoreCase))
                {
                    CacheTranslation(cacheKey, existing.TranslatedTextValue);
                    return existing.TranslatedTextValue;
                }

                var translated = await _translationProvider.TranslateAsync(sanitizedSource, normalizedSource, normalizedTarget, cancellationToken);
                var restoredTranslation = RestoreTranslationTokens(
                    string.IsNullOrWhiteSpace(translated) ? sourceText : translated!,
                    placeholderMap);

                if (existing == null)
                {
                    existing = new TranslatedText
                    {
                        EntityType = normalizedEntityType,
                        EntityId = normalizedEntityId,
                        Field = normalizedField,
                        SourceLanguage = normalizedSource,
                        TargetLanguage = normalizedTarget,
                        SourceText = sourceText,
                        SourceTextHash = hash,
                        TranslatedTextValue = restoredTranslation,
                        LastUpdatedUtc = DateTime.UtcNow
                    };
                    _context.TranslatedTexts.Add(existing);
                }
                else
                {
                    existing.SourceText = sourceText;
                    existing.SourceTextHash = hash;
                    existing.SourceLanguage = normalizedSource;
                    existing.TranslatedTextValue = restoredTranslation;
                    existing.LastUpdatedUtc = DateTime.UtcNow;
                    _context.TranslatedTexts.Update(existing);
                }

                await _context.SaveChangesAsync(cancellationToken);
                CacheTranslation(cacheKey, restoredTranslation);
                return restoredTranslation;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to translate dynamic text for {EntityType}#{EntityId} ({Field})", normalizedEntityType, normalizedEntityId, normalizedField);
                if (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                CacheTranslation(cacheKey, sourceText);
                return sourceText;
            }
        }

        private static string NormalizeLanguage(string? languageCode)
        {
            if (string.IsNullOrWhiteSpace(languageCode))
            {
                return LanguageConstants.English;
            }

            languageCode = languageCode.Trim().ToLowerInvariant();
            return LanguageConstants.SupportedLanguages.Any(l => string.Equals(l.Code, languageCode, StringComparison.OrdinalIgnoreCase))
                ? languageCode
                : LanguageConstants.English;
        }

        private static string ComputeHash(string input)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(input);
            var hashBytes = sha.ComputeHash(bytes);
            var sb = new StringBuilder(hashBytes.Length * 2);
            foreach (var b in hashBytes)
            {
                sb.Append(b.ToString("x2"));
            }

            return sb.ToString();
        }

        private static string BuildCacheKey(string entityType, string entityId, string field, string sourceLanguage, string targetLanguage, string sourceHash)
        {
            return $"dynamic::{entityType}::{entityId}::{field}::{sourceLanguage}->{targetLanguage}::{sourceHash}";
        }

        private void CacheTranslation(string cacheKey, string value)
        {
            if (string.IsNullOrWhiteSpace(cacheKey))
            {
                return;
            }

            var options = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6),
                SlidingExpiration = TimeSpan.FromHours(1)
            };

            _memoryCache.Set(cacheKey, value, options);
        }

        private static string ProtectTranslationTokens(string input, out Dictionary<string, string> placeholderMap)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            placeholderMap = map;
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            var tokenToPlaceholder = new Dictionary<string, string>(StringComparer.Ordinal);
            var counter = 0;

            string ReplaceTokens(string text, Regex regex)
            {
                return regex.Replace(text, match =>
                {
                    var token = match.Value;
                    if (!tokenToPlaceholder.TryGetValue(token, out var placeholder))
                    {
                        placeholder = string.Format(CultureInfo.InvariantCulture, PlaceholderFormat, counter++);
                        tokenToPlaceholder[token] = placeholder;
                        map[placeholder] = token;
                    }
                    return placeholder;
                });
            }

            var result = input;
            result = ReplaceTokens(result, CurlyTokenRegex);
            result = ReplaceTokens(result, HtmlTagRegex);
            result = ReplaceTokens(result, UrlRegex);
            return result;
        }

        private static string RestoreTranslationTokens(string translated, Dictionary<string, string> placeholderMap)
        {
            if (string.IsNullOrEmpty(translated) || placeholderMap.Count == 0)
            {
                return translated;
            }

            var builder = new StringBuilder(translated);
            foreach (var kvp in placeholderMap)
            {
                builder.Replace(kvp.Key, kvp.Value);
            }

            return builder.ToString();
        }
    }
}
