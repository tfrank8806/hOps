using System;
using System.Linq;

namespace hOps.web.Utilities
{
    internal static class SensitiveContentGuard
    {
        private static readonly string[] RestrictedMarkers = new[]
        {
            "password=",
            "pwd=",
            "ssn",
            "social security",
            "credit card",
            "api key",
            "secret key",
            "private key",
            "token=",
            "bearer ",
            "stack trace",
            "exception:",
            "connection string"
        };

        public static bool ContainsSensitiveData(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            var normalized = input.ToLowerInvariant();
            return RestrictedMarkers.Any(marker => normalized.Contains(marker));
        }

        public static string Sanitize(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            return input.Trim();
        }
    }
}
