using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using hOps.web.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace hOps.web.Services
{
    public class GoogleRecaptchaValidator : ICaptchaValidator
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<GoogleRecaptchaValidator> _logger;
        private readonly CaptchaOptions _options;

        public GoogleRecaptchaValidator(
            HttpClient httpClient,
            IOptions<CaptchaOptions> options,
            ILogger<GoogleRecaptchaValidator> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _options = options.Value ?? new CaptchaOptions();
        }

        public async Task<bool> ValidateAsync(string token, string? remoteIp, CancellationToken cancellationToken)
        {
            if (!_options.Enabled)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(_options.SecretKey))
            {
                _logger.LogWarning("Captcha is enabled but SecretKey is not configured.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            var values = new List<KeyValuePair<string, string>>
            {
                new("secret", _options.SecretKey),
                new("response", token)
            };

            if (!string.IsNullOrWhiteSpace(remoteIp))
            {
                values.Add(new("remoteip", remoteIp));
            }

            try
            {
                using var content = new FormUrlEncodedContent(values);
                using var response = await _httpClient.PostAsync("https://www.google.com/recaptcha/api/siteverify", content, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Captcha verification HTTP failure: {StatusCode}", response.StatusCode);
                    return false;
                }

                var payload = await response.Content.ReadFromJsonAsync<RecaptchaResponse>(cancellationToken: cancellationToken);
                if (payload?.Success == true)
                {
                    return true;
                }

                if (payload?.ErrorCodes is { Length: > 0 })
                {
                    _logger.LogWarning("Captcha verification failed: {Errors}", string.Join(",", payload.ErrorCodes));
                }

                return false;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _logger.LogError(ex, "Captcha verification request failed.");
                return false;
            }
        }

        private sealed class RecaptchaResponse
        {
            [JsonPropertyName("success")]
            public bool Success { get; set; }

            [JsonPropertyName("error-codes")]
            public string[]? ErrorCodes { get; set; }
        }
    }
}
