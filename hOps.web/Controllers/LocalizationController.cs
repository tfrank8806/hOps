using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using hOps.web.Services.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace hOps.web.Controllers
{
    [AllowAnonymous]
    public class LocalizationController : Controller
    {
        private readonly ILanguagePreferenceService _languagePreferenceService;
        private readonly ITranslationService _translationService;

        public LocalizationController(
            ILanguagePreferenceService languagePreferenceService,
            ITranslationService translationService)
        {
            _languagePreferenceService = languagePreferenceService;
            _translationService = translationService;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetLanguage([FromForm] string languageCode, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(languageCode))
            {
                return BadRequest(new { error = "Language code is required." });
            }

            await _languagePreferenceService.SetPreferredLanguageAsync(User, languageCode, cancellationToken);

            var normalizedLanguage = _translationService.SupportedLanguages
                .FirstOrDefault(l => string.Equals(l.Code, languageCode, StringComparison.OrdinalIgnoreCase))
                ?.Code ?? _translationService.DefaultLanguage;

            var displayName = _translationService.SupportedLanguages
                .FirstOrDefault(l => string.Equals(l.Code, normalizedLanguage, StringComparison.OrdinalIgnoreCase))
                ?.DisplayName ?? normalizedLanguage;

            return Json(new
            {
                success = true,
                language = normalizedLanguage,
                displayName
            });
        }
    }
}
