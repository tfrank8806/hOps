using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Markdig;

namespace hOps.web.Utilities
{
    public static class RichTextRenderer
    {
        private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseSoftlineBreakAsHardlineBreak()
            .DisableHtml()
            .Build();

        private static readonly IReadOnlyDictionary<string, string> AllowedColorClasses = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["red"] = "rich-text-color-red",
            ["orange"] = "rich-text-color-orange",
            ["yellow"] = "rich-text-color-yellow",
            ["green"] = "rich-text-color-green",
            ["teal"] = "rich-text-color-teal",
            ["blue"] = "rich-text-color-blue",
            ["purple"] = "rich-text-color-purple",
            ["pink"] = "rich-text-color-pink",
            ["gray"] = "rich-text-color-gray"
        };
        private static readonly IReadOnlyDictionary<string, string> EmailColorHexByClass = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["rich-text-color-red"] = "#b42318",
            ["rich-text-color-orange"] = "#c2410c",
            ["rich-text-color-yellow"] = "#b58105",
            ["rich-text-color-green"] = "#15803d",
            ["rich-text-color-teal"] = "#0f766e",
            ["rich-text-color-blue"] = "#2563eb",
            ["rich-text-color-purple"] = "#7c3aed",
            ["rich-text-color-pink"] = "#db2777",
            ["rich-text-color-gray"] = "#334155"
        };

        private static readonly Regex ColorTagRegex = new(@"\{\{(/?color(?::(?<name>[a-z0-9-]+))?)\}\}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex HtmlBreakTagRegex = new(@"(<br\s*/?>|</p>|</div>|</li>|</ul>|</ol>)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex HtmlTagRegex = new(@"<[^>]+>", RegexOptions.Compiled);
        private static readonly Regex CollapseWhitespaceRegex = new(@"\s+", RegexOptions.Compiled);
        private static readonly Regex RichTextSpanRegex = new("<span class=\"(?<classes>[^\"]*rich-text[^\"]*)\">", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex MarkTagRegex = new("<mark(\\s+[^>]*)?>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static string ToHtml(string? content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return string.Empty;
            }

            var text = MentionMarkupFormatter.ReplaceMentionsWithPlaceholders(content, out List<MentionMarkupFormatter.MentionToken> mentionTokens);
            if (string.IsNullOrEmpty(text))
            {
                text = string.Empty;
            }

            text = text.Replace("\r\n", "\n", StringComparison.Ordinal);

            var processedText = ProcessColorMarkup(text, out List<ColorToken> colorTokens);

            var html = Markdown.ToHtml(processedText, Pipeline);
            html = ApplyInlineFallback(html);

            if (colorTokens.Count > 0)
            {
                html = ApplyColorTokens(html, colorTokens);
            }

            if (mentionTokens.Count > 0)
            {
                foreach (var mentionToken in mentionTokens)
                {
                    var name = WebUtility.HtmlEncode(mentionToken.DisplayName);
                    var userId = WebUtility.UrlEncode(mentionToken.UserId);
                    var anchor = $"<a class=\"mention\" href=\"/DirectMessages?userId={userId}\">@{name}</a>";
                    html = html.Replace(mentionToken.Placeholder, anchor, StringComparison.Ordinal);
                }
            }

            return html.Trim();
        }

        private static string ProcessColorMarkup(string text, out List<ColorToken> tokens)
        {
            tokens = new List<ColorToken>();

            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var matches = ColorTagRegex.Matches(text);
            if (matches.Count == 0)
            {
                return text;
            }

            var builder = new StringBuilder();
            var stack = new Stack<ColorToken>();
            var lastIndex = 0;

            foreach (Match match in matches)
            {
                builder.Append(text, lastIndex, match.Index - lastIndex);
                lastIndex = match.Index + match.Length;

                var isClosing = match.Value.StartsWith("{{/color", StringComparison.OrdinalIgnoreCase);
                if (isClosing)
                {
                    if (stack.Count == 0)
                    {
                        builder.Append(match.Value);
                        continue;
                    }

                    var openToken = stack.Pop();
                    openToken.MarkClosed();
                    builder.Append(openToken.EndPlaceholder);
                    continue;
                }

                var colorName = match.Groups["name"].Value;
                if (!AllowedColorClasses.TryGetValue(colorName, out var cssClass))
                {
                    builder.Append(match.Value);
                    continue;
                }

                var colorToken = new ColorToken(tokens.Count, colorName, cssClass);
                tokens.Add(colorToken);
                stack.Push(colorToken);
                builder.Append(colorToken.StartPlaceholder);
            }

            if (lastIndex < text.Length)
            {
                builder.Append(text, lastIndex, text.Length - lastIndex);
            }

            var processed = builder.ToString();

            if (tokens.Count == 0)
            {
                return processed;
            }

            foreach (var incompleteToken in tokens.Where(t => !t.IsClosed))
            {
                processed = processed.Replace(incompleteToken.StartPlaceholder, incompleteToken.OriginalStartTag, StringComparison.Ordinal);
            }

            tokens = tokens.Where(t => t.IsClosed).ToList();

            return processed;
        }

        private static string ApplyColorTokens(string html, List<ColorToken> tokens)
        {
            if (string.IsNullOrEmpty(html))
            {
                return string.Empty;
            }

            if (tokens.Count == 0)
            {
                return html;
            }

            foreach (var colorToken in tokens)
            {
                var startTag = $"<span class=\"rich-text-color {colorToken.CssClass}\">";
                html = html.Replace(colorToken.StartPlaceholder, startTag, StringComparison.Ordinal);
                html = html.Replace(colorToken.EndPlaceholder, "</span>", StringComparison.Ordinal);
            }

            return html;
        }

        public static string ToEmailHtml(string? content)
        {
            var html = ToHtml(content);
            if (string.IsNullOrWhiteSpace(html))
            {
                return string.Empty;
            }

            return ApplyEmailInlineStyles(html);
        }

        public static string ToPlainText(string? content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return string.Empty;
            }

            var html = ToHtml(content);
            if (string.IsNullOrWhiteSpace(html))
            {
                return string.Empty;
            }

            var withBreaks = HtmlBreakTagRegex.Replace(html, "\n");
            var stripped = HtmlTagRegex.Replace(withBreaks, " ");
            var decoded = WebUtility.HtmlDecode(stripped);
            var collapsed = CollapseWhitespaceRegex.Replace(decoded, " ").Trim();
            return collapsed;
        }

        public static string ToPlainTextWithLineBreaks(string? content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return string.Empty;
            }

            var html = ToHtml(content);
            if (string.IsNullOrWhiteSpace(html))
            {
                return string.Empty;
            }

            var withBreaks = HtmlBreakTagRegex.Replace(html, "\n");
            var stripped = HtmlTagRegex.Replace(withBreaks, " ");
            var decoded = WebUtility.HtmlDecode(stripped);
            var normalized = decoded.Replace("\r\n", "\n", StringComparison.Ordinal);

            var lines = normalized
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line));

            return string.Join("\n", lines);
        }

        private sealed class ColorToken
        {
            public ColorToken(int index, string colorName, string cssClass)
            {
                Index = index;
                ColorName = colorName;
                CssClass = cssClass;
                StartPlaceholder = $"[[COLOR{index}]]";
                EndPlaceholder = $"[[/COLOR{index}]]";
                OriginalStartTag = $"{{{{color:{colorName}}}}}";
            }

            public int Index { get; }
            public string ColorName { get; }
            public string CssClass { get; }
            public string StartPlaceholder { get; }
            public string EndPlaceholder { get; }
            public string OriginalStartTag { get; }
            public bool IsClosed { get; private set; }

            public void MarkClosed()
            {
                IsClosed = true;
            }
        }
        private static string ApplyInlineFallback(string html)
        {
            if (string.IsNullOrEmpty(html))
            {
                return html;
            }

            static string Replace(string input, string pattern, Func<Match, string> replacement)
            {
                return Regex.Replace(
                    input,
                    pattern,
                    match => replacement(match),
                    RegexOptions.Singleline | RegexOptions.Compiled);
            }

            html = Replace(html, @"\*\*(.+?)\*\*", m => $"<strong>{m.Groups[1].Value}</strong>");

            html = Replace(html, @"(?<!\*)\*(?!\*)(.+?)(?<!\*)\*(?!\*)", m => $"<em>{m.Groups[1].Value}</em>");

            html = Replace(html, @"\+\+(.+?)\+\+", m => $"<span class=\"rich-text-underline\">{m.Groups[1].Value}</span>");

            html = Replace(html, @"\~\~(.+?)\~\~", m => $"<del>{m.Groups[1].Value}</del>");

            html = Replace(html, @"\=\=(.+?)\=\=", m => $"<mark>{m.Groups[1].Value}</mark>");

            return html;
        }

        private static string ApplyEmailInlineStyles(string html)
        {
            if (string.IsNullOrEmpty(html))
            {
                return string.Empty;
            }

            html = RichTextSpanRegex.Replace(html, match =>
            {
                var classes = match.Groups["classes"].Value
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (classes.Length == 0)
                {
                    return match.Value;
                }

                var styles = new StringBuilder();
                foreach (var className in classes)
                {
                    if (className.Equals("rich-text-underline", StringComparison.OrdinalIgnoreCase))
                    {
                        styles.Append("text-decoration:underline;");
                    }

                    if (EmailColorHexByClass.TryGetValue(className, out var colorHex))
                    {
                        styles.Append($"color:{colorHex};");
                    }
                }

                if (styles.Length == 0)
                {
                    return match.Value;
                }

                return $"<span style=\"{styles}\">";
            });

            html = MarkTagRegex.Replace(
                html,
                "<mark style=\"background-color:#fff2a8;padding:0 0.2em;border-radius:0.2em;\">");

            return html;
        }
    }
}
