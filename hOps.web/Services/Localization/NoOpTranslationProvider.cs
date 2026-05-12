using System.Threading;
using System.Threading.Tasks;

namespace hOps.web.Services.Localization
{
    public sealed class NoOpTranslationProvider : IExternalTranslationProvider
    {
        public Task<string?> TranslateAsync(string text, string sourceLanguage, string targetLanguage, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<string?>(null);
        }
    }
}
