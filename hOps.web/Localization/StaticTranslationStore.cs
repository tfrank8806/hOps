using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace hOps.web.Localization
{
    public sealed class StaticTranslationStore
    {
        private readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, string>> _cache = new(StringComparer.OrdinalIgnoreCase);
        private readonly IHostEnvironment _environment;
        private readonly ILogger<StaticTranslationStore> _logger;

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        public StaticTranslationStore(IHostEnvironment environment, ILogger<StaticTranslationStore> logger)
        {
            _environment = environment;
            _logger = logger;
        }

        public IReadOnlyDictionary<string, string> GetTranslations(string languageCode)
        {
            var normalized = (languageCode ?? LanguageConstants.English).Trim().ToLowerInvariant();
            return _cache.GetOrAdd(normalized, LoadTranslations);
        }

        public void Clear() => _cache.Clear();

        private IReadOnlyDictionary<string, string> LoadTranslations(string languageCode)
        {
            try
            {
                var fileName = $"static.{languageCode}.json";
                var path = Path.Combine(_environment.ContentRootPath, "Localization", fileName);

                if (!File.Exists(path))
                {
                    _logger.LogWarning("Static translation file {File} was not found for language {Language}", fileName, languageCode);
                    return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }

                var json = File.ReadAllText(path);
                var dictionary = JsonSerializer.Deserialize<Dictionary<string, string>>(json, SerializerOptions);
                if (dictionary == null)
                {
                    return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }

                var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in dictionary)
                {
                    if (!result.ContainsKey(kvp.Key))
                    {
                        result[kvp.Key] = kvp.Value;
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to load static translations for language {Language}", languageCode);
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
        }
    }
}
