using System.Collections.Generic;

namespace hOps.web.Localization
{
    public static class LanguageConstants
    {
        public const string English = "en";
        public const string Spanish = "es";

        public static readonly IReadOnlyList<LanguageOption> SupportedLanguages = new[]
        {
            new LanguageOption(English, "English"),
            new LanguageOption(Spanish, "Español")
        };
    }
}
