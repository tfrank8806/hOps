using System;
using System.Collections.Generic;
using System.Net;
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

        public static string ToHtml(string? content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return string.Empty;
            }

            var text = MentionMarkupFormatter.ReplaceMentionsWithPlaceholders(content, out List<MentionMarkupFormatter.MentionToken> tokens);
            if (string.IsNullOrEmpty(text))
            {
                text = string.Empty;
            }

            text = text.Replace("\r\n", "\n", StringComparison.Ordinal);

            var html = Markdown.ToHtml(text, Pipeline);

            if (tokens.Count > 0)
            {
                foreach (var token in tokens)
                {
                    var name = WebUtility.HtmlEncode(token.DisplayName);
                    var userId = WebUtility.UrlEncode(token.UserId);
                    var anchor = $"<a class=\"mention\" href=\"/DirectMessages?userId={userId}\">@{name}</a>";
                    html = html.Replace(token.Placeholder, anchor, StringComparison.Ordinal);
                }
            }

            return html.Trim();
        }
    }
}
