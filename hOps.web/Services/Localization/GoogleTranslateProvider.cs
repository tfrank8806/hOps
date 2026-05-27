using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace hOps.web.Services.Localization
{
    public sealed class GoogleTranslateProvider : IExternalTranslationProvider
    {
        private const string Endpoint = "https://translate.googleapis.com/translate_a/single";
        private readonly HttpClient _httpClient;
        private readonly ILogger<GoogleTranslateProvider> _logger;
        public GoogleTranslateProvider(HttpClient httpClient, ILogger<GoogleTranslateProvider> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<string?> TranslateAsync(string text, string sourceLanguage, string targetLanguage, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var normalizedSource = (sourceLanguage ?? "en").Trim().ToLowerInvariant();
            var normalizedTarget = (targetLanguage ?? "en").Trim().ToLowerInvariant();

            if (string.Equals(normalizedSource, normalizedTarget, StringComparison.OrdinalIgnoreCase))
            {
                return text;
            }

            var builder = new StringBuilder();
            foreach (var chunk in SplitIntoChunks(text, 4500))
            {
                var translated = await TranslateChunkAsync(chunk, normalizedSource, normalizedTarget, cancellationToken);
                builder.Append(translated ?? chunk);
            }

            return builder.ToString();
        }

        private async Task<string?> TranslateChunkAsync(string text, string sourceLanguage, string targetLanguage, CancellationToken cancellationToken)
        {
            var query = new StringBuilder(Endpoint)
                .Append("?client=gtx")
                .Append("&dt=t")
                .Append("&sl=").Append(Uri.EscapeDataString(sourceLanguage))
                .Append("&tl=").Append(Uri.EscapeDataString(targetLanguage))
                .Append("&q=").Append(Uri.EscapeDataString(text))
                .ToString();

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, query);
                using var response = await _httpClient.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Translation request failed with status {StatusCode}", response.StatusCode);
                    return null;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                var documentOptions = new JsonDocumentOptions { AllowTrailingCommas = true };
                using var document = await JsonDocument.ParseAsync(stream, documentOptions, cancellationToken);
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                {
                    return null;
                }

                var builder = new StringBuilder();
                if (document.RootElement.GetArrayLength() > 0)
                {
                    var segments = document.RootElement[0];
                    if (segments.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var segment in segments.EnumerateArray())
                        {
                            if (segment.ValueKind == JsonValueKind.Array && segment.GetArrayLength() > 0)
                            {
                                var value = segment[0].GetString();
                                if (!string.IsNullOrWhiteSpace(value))
                                {
                                    builder.Append(value);
                                }
                            }
                        }
                    }
                }

                return builder.Length > 0 ? builder.ToString() : null;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error translating text chunk");
                return null;
            }
        }

        private static IEnumerable<string> SplitIntoChunks(string text, int maxLength)
        {
            if (text.Length <= maxLength)
            {
                yield return text;
                yield break;
            }

            var position = 0;
            while (position < text.Length)
            {
                var length = Math.Min(maxLength, text.Length - position);
                var chunk = text.Substring(position, length);

                // try to break on whitespace if possible
                if (position + length < text.Length)
                {
                    var lastSpace = chunk.LastIndexOf(' ');
                    if (lastSpace > maxLength * 0.6)
                    {
                        length = lastSpace + 1;
                        chunk = text.Substring(position, length);
                    }
                }

                yield return chunk;
                position += length;
            }
        }
    }
}
