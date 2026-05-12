using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using hOps.web.Localization;

namespace hOps.web.Services.Localization
{
    public interface ITranslationService
    {
        string DefaultLanguage { get; }
        IReadOnlyList<LanguageOption> SupportedLanguages { get; }

        string Translate(string key, string targetLanguage, string? fallback = null);
        bool TryTranslate(string key, string targetLanguage, out string translation);
        Task<string> TranslateDynamicAsync(
            string entityType,
            string entityId,
            string field,
            string sourceText,
            string sourceLanguage,
            string targetLanguage,
            CancellationToken cancellationToken = default);
    }
}
