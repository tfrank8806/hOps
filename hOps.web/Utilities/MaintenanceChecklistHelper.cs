#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace hOps.web.Utilities
{
    public static class MaintenanceChecklistHelper
    {
        public static List<string> ParseAreaOptions(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<string>();
            }

            try
            {
                var options = JsonSerializer.Deserialize<List<string>>(json);
                if (options == null)
                {
                    return new List<string>();
                }

                return options
                    .Select(NormalizeAreaLabel)
                    .Where(label => !string.IsNullOrWhiteSpace(label))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(label => label!)
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        public static string BuildAreaOptionsJson(IEnumerable<string> labels)
        {
            var normalized = labels
                .Select(NormalizeAreaLabel)
                .Where(label => !string.IsNullOrWhiteSpace(label))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(label => label!)
                .ToList();

            return JsonSerializer.Serialize(normalized);
        }

        public static string? NormalizeAreaLabel(string? label)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                return null;
            }

            var trimmed = label.Trim();
            return trimmed.Length > 160 ? trimmed[..160] : trimmed;
        }
    }
}
