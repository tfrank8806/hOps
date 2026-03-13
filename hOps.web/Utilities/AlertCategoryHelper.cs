using System;
using System.Collections.Generic;

namespace hOps.web.Utilities
{
    public static class AlertCategoryHelper
    {
        public const string OtherKey = "other";

        private static readonly Dictionary<string, (string Label, int Order)> Definitions = new(StringComparer.OrdinalIgnoreCase)
        {
            ["workorder"] = ("Work Orders", 0),
            ["passon-log"] = ("Pass-on Logs", 1),
            ["log"] = ("Mail Logs", 2),
            ["schedule"] = ("Schedules & Time Off", 3),
            ["mention"] = ("Mentions", 4),
            [OtherKey] = ("Other Alerts", 99)
        };

        private static readonly string[] KnownKeysCache = BuildKnownKeysArray();
        private static readonly string[] KnownNonOtherKeysCache = BuildKnownNonOtherKeysArray();

        public static IReadOnlyCollection<string> KnownKeys => KnownKeysCache;
        public static IReadOnlyCollection<string> KnownNonOtherKeys => KnownNonOtherKeysCache;

        public static AlertCategoryDefinition Resolve(string? rawType)
        {
            var normalized = NormalizeKey(rawType);
            var definition = Definitions[normalized];
            return new AlertCategoryDefinition(normalized, definition.Label, definition.Order);
        }

        public static string NormalizeKey(string? key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return OtherKey;
            }

            var normalized = key.Trim().ToLowerInvariant();
            return Definitions.ContainsKey(normalized)
                ? normalized
                : OtherKey;
        }

        public static bool BelongsToCategory(string? rawType, string categoryKey)
        {
            var normalizedCategory = NormalizeKey(categoryKey);
            var resolved = Resolve(rawType);
            return string.Equals(resolved.Key, normalizedCategory, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsKnownRawType(string? rawType)
        {
            if (string.IsNullOrWhiteSpace(rawType))
            {
                return false;
            }

            var normalized = rawType.Trim().ToLowerInvariant();
            return Definitions.ContainsKey(normalized) && !normalized.Equals(OtherKey, StringComparison.OrdinalIgnoreCase);
        }

        private static string[] BuildKnownKeysArray()
        {
            var keys = new List<string>(Definitions.Keys.Count);
            foreach (var key in Definitions.Keys)
            {
                keys.Add(key);
            }

            return keys.ToArray();
        }

        private static string[] BuildKnownNonOtherKeysArray()
        {
            var keys = new List<string>();
            foreach (var key in Definitions.Keys)
            {
                if (key.Equals(OtherKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                keys.Add(key);
            }

            return keys.ToArray();
        }

        public readonly record struct AlertCategoryDefinition(string Key, string Label, int Order);
    }
}
