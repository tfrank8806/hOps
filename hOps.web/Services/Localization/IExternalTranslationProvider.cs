using System.Threading;
using System.Threading.Tasks;

namespace hOps.web.Services.Localization
{
    public interface IExternalTranslationProvider
    {
        Task<string?> TranslateAsync(string text, string sourceLanguage, string targetLanguage, CancellationToken cancellationToken = default);
    }
}
