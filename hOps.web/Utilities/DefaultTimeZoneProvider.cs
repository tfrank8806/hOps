using System;
using System.Collections.Generic;
using System.Linq;

namespace hOps.web.Utilities
{
    public static class DefaultTimeZoneProvider
    {
        public const string WindowsId = "Central Standard Time";
        public const string IanaId = "America/Chicago";

        private static readonly string[] LegacyUtcIds = new[]
        {
            "UTC",
            "Coordinated Universal Time"
        };

        public static string DefaultTimeZoneId => OperatingSystem.IsWindows() ? WindowsId : IanaId;

        public static string GetEffectiveTimeZoneId(string? timeZoneId)
        {
            if (string.IsNullOrWhiteSpace(timeZoneId))
            {
                return DefaultTimeZoneId;
            }

            var trimmed = timeZoneId.Trim();

            if (string.Equals(trimmed, TimeZoneInfo.Utc.Id, StringComparison.OrdinalIgnoreCase))
            {
                return DefaultTimeZoneId;
            }

            if (LegacyUtcIds.Any(id => string.Equals(trimmed, id, StringComparison.OrdinalIgnoreCase)))
            {
                return DefaultTimeZoneId;
            }

            return trimmed;
        }

        public static string NormalizeForStorage(string? timeZoneId)
        {
            var effective = GetEffectiveTimeZoneId(timeZoneId);
            var candidates = new[]
            {
                effective,
                WindowsId,
                IanaId,
                TimeZoneInfo.Utc.Id,
                "UTC",
                "Coordinated Universal Time"
            };

            foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(candidate).Id;
                }
                catch (TimeZoneNotFoundException)
                {
                    // Try next option
                }
                catch (InvalidTimeZoneException)
                {
                    // Try next option
                }
            }

            return TimeZoneInfo.Utc.Id;
        }
    }
}
