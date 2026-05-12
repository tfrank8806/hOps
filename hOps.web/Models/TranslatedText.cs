using System;

namespace hOps.web.Models
{
    public class TranslatedText
    {
        public int Id { get; set; }
        public string EntityType { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public string Field { get; set; } = string.Empty;
        public string SourceLanguage { get; set; } = "en";
        public string TargetLanguage { get; set; } = string.Empty;
        public string SourceTextHash { get; set; } = string.Empty;
        public string SourceText { get; set; } = string.Empty;
        public string TranslatedTextValue { get; set; } = string.Empty;
        public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
    }
}
