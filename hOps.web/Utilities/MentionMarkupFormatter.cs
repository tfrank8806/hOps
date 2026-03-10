using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace hOps.web.Utilities
{
    public static class MentionMarkupFormatter
    {
        private const char StartMarker = '\u200D';
        private const char EndMarker = '\u200E';
        private const char ZeroWidthZero = '\u200B';
        private const char ZeroWidthOne = '\u200C';

        private static readonly Regex MentionRegex = new Regex(
            @"@\[(?<name>[^\]]+)\]\(user:(?<id>[^\)]+)\)|@(?<plainName>[^" + StartMarker + @"@]+)" + StartMarker + @"(?<plainId>[" + ZeroWidthZero + ZeroWidthOne + @"]+)" + EndMarker,
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        public static IEnumerable<MentionReference> ExtractMentions(string? content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return Enumerable.Empty<MentionReference>();
            }

            return MentionRegex
                .Matches(content)
                .Cast<Match>()
                .Select(match => new MentionReference(
                    DecodeIdentifier(GetGroupValue(match, "id", "plainId")),
                    GetGroupValue(match, "name", "plainName")))
                .Where(reference => !string.IsNullOrWhiteSpace(reference.UserId))
                .GroupBy(reference => reference.UserId, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First());
        }

        public static string RemoveDuplicateMentions(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return string.Empty;
            }

            var seenPairs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            return MentionRegex.Replace(content, match =>
            {
                var id = DecodeIdentifier(GetGroupValue(match, "id", "plainId"));
                var name = GetGroupValue(match, "name", "plainName");
                var key = $"{id}:{name}";
                if (seenPairs.Add(key))
                {
                    return match.Value;
                }

                return string.Empty;
            });
        }

        public static string ToDisplayText(string? content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return string.Empty;
            }

            return MentionRegex.Replace(content, match => "@" + GetGroupValue(match, "name", "plainName"));
        }

        public static string ToHtml(string? content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return string.Empty;
            }

            return MentionRegex.Replace(content, match =>
            {
                var name = System.Net.WebUtility.HtmlEncode(GetGroupValue(match, "name", "plainName"));
                var identifier = DecodeIdentifier(GetGroupValue(match, "id", "plainId"));
                if (identifier.StartsWith("department:", StringComparison.OrdinalIgnoreCase))
                {
                    return $"<span class=\"mention mention-department\">@{name}</span>";
                }

                var userId = System.Net.WebUtility.UrlEncode(identifier);
                return $"<a class=\"mention\" href=\"/DirectMessages?userId={userId}\">@{name}</a>";
            });
        }

        public static string ReplaceMentionsWithPlaceholders(string? content, out List<MentionToken> tokens)
        {
            if (string.IsNullOrEmpty(content))
            {
                tokens = new List<MentionToken>();
                return string.Empty;
            }

            var index = 0;
            var collected = new List<MentionToken>();
            var replaced = MentionRegex.Replace(content, match =>
            {
                var name = GetGroupValue(match, "name", "plainName");
                var userId = DecodeIdentifier(GetGroupValue(match, "id", "plainId"));
                var placeholder = $"[[MENTION_{index++}]]";
                collected.Add(new MentionToken(placeholder, name, userId));
                return placeholder;
            });

            tokens = collected;
            return replaced;
        }

        private static string GetGroupValue(Match match, string primaryGroup, string fallbackGroup)
        {
            var value = match.Groups[primaryGroup].Value;
            if (!string.IsNullOrEmpty(value))
            {
                return value.Trim();
            }

            return match.Groups[fallbackGroup].Value.Trim();
        }

        private static string DecodeIdentifier(string encoded)
        {
            if (string.IsNullOrEmpty(encoded))
            {
                return string.Empty;
            }

            var bits = new List<char>(encoded.Length);
            foreach (var ch in encoded)
            {
                if (ch == ZeroWidthZero)
                {
                    bits.Add('0');
                }
                else if (ch == ZeroWidthOne)
                {
                    bits.Add('1');
                }
            }

            if (bits.Count == 0)
            {
                return encoded;
            }

            var chars = new List<char>(bits.Count / 8);
            for (var i = 0; i + 7 < bits.Count; i += 8)
            {
                var chunk = new string(bits.Skip(i).Take(8).ToArray());
                var code = Convert.ToInt32(chunk, 2);
                chars.Add((char)code);
            }

            return new string(chars.ToArray());
        }

        public readonly record struct MentionReference(string UserId, string DisplayName);
        public readonly record struct MentionToken(string Placeholder, string DisplayName, string UserId);
    }
}
