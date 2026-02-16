#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using hOps.web.Models;

namespace hOps.web.Utilities
{
    public static class MaintenanceLogTemplateHelper
    {
        private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private static readonly DayOfWeek[] OrderedWeekDays = new[]
        {
            DayOfWeek.Sunday,
            DayOfWeek.Monday,
            DayOfWeek.Tuesday,
            DayOfWeek.Wednesday,
            DayOfWeek.Thursday,
            DayOfWeek.Friday,
            DayOfWeek.Saturday
        };

        public static List<MaintenanceLogColumnDefinition> ParseColumns(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<MaintenanceLogColumnDefinition>();
            }

            try
            {
                var parsed = JsonSerializer.Deserialize<List<MaintenanceLogColumnDefinition>>(json, SerializerOptions);
                if (parsed == null)
                {
                    return new List<MaintenanceLogColumnDefinition>();
                }

                return parsed
                    .Select(SanitizeColumn)
                    .Where(column => column != null)
                    .Select(column => column!)
                    .ToList();
            }
            catch
            {
                return new List<MaintenanceLogColumnDefinition>();
            }
        }

        public static string BuildColumnsJson(IEnumerable<MaintenanceLogColumnDefinition> columns)
        {
            var sanitized = columns
                .Select(SanitizeColumn)
                .Where(column => column != null)
                .Select(column => column!)
                .GroupBy(column => column.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();

            return JsonSerializer.Serialize(sanitized, SerializerOptions);
        }

        public static int BuildWeeklyBitmask(IEnumerable<DayOfWeek> days)
        {
            var mask = 0;
            foreach (var day in days.Distinct())
            {
                var bit = 1 << (int)day;
                mask |= bit;
            }

            return mask;
        }

        public static IReadOnlyList<DayOfWeek> ParseWeeklyBitmask(int bitmask)
        {
            var days = new List<DayOfWeek>();
            foreach (var day in OrderedWeekDays)
            {
                var bit = 1 << (int)day;
                if ((bitmask & bit) == bit)
                {
                    days.Add(day);
                }
            }

            return days;
        }

        public static string BuildScheduleSummary(MaintenanceLogTemplate template)
        {
            switch (template.ScheduleType)
            {
                case MaintenanceLogScheduleType.Daily:
                    return template.DueTimeLocal.HasValue
                        ? $"Daily by {FormatTime(template.DueTimeLocal.Value)}"
                        : "Daily";
                case MaintenanceLogScheduleType.Weekly:
                    var days = ParseWeeklyBitmask(template.WeeklyDaysBitmask);
                    var dayText = days.Any()
                        ? string.Join(", ", days.Select(d => d.ToString()))
                        : "Weekly";
                    if (template.DueTimeLocal.HasValue)
                    {
                        return $"{dayText} by {FormatTime(template.DueTimeLocal.Value)}";
                    }

                    return dayText;
                case MaintenanceLogScheduleType.Monthly:
                    var day = template.DayOfMonth ?? 1;
                    var ordinal = GetOrdinal(day);
                    if (template.DueTimeLocal.HasValue)
                    {
                        return $"Monthly on the {ordinal} by {FormatTime(template.DueTimeLocal.Value)}";
                    }

                    return $"Monthly on the {ordinal}";
                default:
                    return "No schedule";
            }
        }

        public static string NormalizeColumnKey(string? key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            var trimmed = new string(key
                .Where(char.IsLetterOrDigit)
                .ToArray())
                .ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(trimmed))
            {
                trimmed = $"column{Guid.NewGuid():N}".Substring(0, 8);
            }

            return trimmed.Length > 32 ? trimmed[..32] : trimmed;
        }

        public static List<string> ParseOptions(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new List<string>();
            }

            return text
                .Split(new[] { '\r', '\n', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(option => option.Trim())
                .Where(option => !string.IsNullOrWhiteSpace(option))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(25)
                .ToList();
        }

        private static MaintenanceLogColumnDefinition? SanitizeColumn(MaintenanceLogColumnDefinition? column)
        {
            if (column == null)
            {
                return null;
            }

            var key = NormalizeColumnKey(column.Key);
            if (string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            var label = string.IsNullOrWhiteSpace(column.Label) ? key : column.Label.Trim();
            if (label.Length > 160)
            {
                label = label[..160];
            }

            var type = string.IsNullOrWhiteSpace(column.Type)
                ? MaintenanceLogColumnDefinition.DefaultColumnType
                : column.Type.Trim().ToLowerInvariant();

            if (!MaintenanceLogColumnDefinition.AllowedTypes.Contains(type))
            {
                type = MaintenanceLogColumnDefinition.DefaultColumnType;
            }

            var options = column.Options
                .Select(option => option?.Trim())
                .Where(option => !string.IsNullOrWhiteSpace(option))
                .Select(option => option!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(25)
                .ToList();

            return new MaintenanceLogColumnDefinition
            {
                Key = key,
                Label = label,
                Type = type,
                Required = column.Required,
                Options = options
            };
        }

        private static string GetOrdinal(int number)
        {
            var abs = Math.Abs(number);
            var lastTwoDigits = abs % 100;
            if (lastTwoDigits is >= 11 and <= 13)
            {
                return $"{number}th";
            }

            var lastDigit = abs % 10;
            return lastDigit switch
            {
                1 => $"{number}st",
                2 => $"{number}nd",
                3 => $"{number}rd",
                _ => $"{number}th"
            };
        }

        private static string FormatTime(TimeSpan time)
        {
            var dateTime = DateTime.Today.Add(time);
            return dateTime.ToString("h:mm tt");
        }
    }

    public sealed class MaintenanceLogColumnDefinition
    {
        public const string DefaultColumnType = "text";

        public static IReadOnlyCollection<string> AllowedTypes { get; } = new[]
        {
            "text",
            "number",
            "checkbox",
            "select"
        };

        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Type { get; set; } = DefaultColumnType;
        public bool Required { get; set; }
        public List<string> Options { get; set; } = new();
    }
}
