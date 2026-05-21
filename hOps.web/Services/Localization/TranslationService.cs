using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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

                var translated = await _translationProvider.TranslateAsync(sourceText, normalizedSource, normalizedTarget, cancellationToken);
                if (string.IsNullOrWhiteSpace(translated))
                {
                    CacheTranslation(cacheKey, sourceText);
                    return sourceText;
                }

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
                        TranslatedTextValue = translated,
                        LastUpdatedUtc = DateTime.UtcNow
                    };
                    _context.TranslatedTexts.Add(existing);
                }
                else
                {
                    existing.SourceText = sourceText;
                    existing.SourceTextHash = hash;
                    existing.SourceLanguage = normalizedSource;
                    existing.TranslatedTextValue = translated;
                    existing.LastUpdatedUtc = DateTime.UtcNow;
                    _context.TranslatedTexts.Update(existing);
                }

                await _context.SaveChangesAsync(cancellationToken);
                CacheTranslation(cacheKey, translated);
                return translated;
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
    }
}
