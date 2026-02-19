using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using hOps.web.Models;

namespace hOps.web.Utilities
{
    public static class PassOnLogEmailHelper
    {
        public static string FormatUserName(string? firstName, string? lastName, string? email)
        {
            var name = $"{firstName} {lastName}".Trim();
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            return string.IsNullOrWhiteSpace(email) ? "Unknown User" : email!;
        }

        public static string FormatUserName(ApplicationUser? user)
        {
            if (user == null)
            {
                return "Unknown User";
            }

            return FormatUserName(user.FirstName, user.LastName, user.Email);
        }

        public static string BuildLogEmailBody(
            PassOnLog log,
            string linkUrl,
            IReadOnlyCollection<string>? propertyNames = null,
            IEnumerable<PassOnLogComment>? comments = null,
            TimeZoneInfo? recipientTimeZone = null)
        {
            var names = propertyNames ?? log.Properties
                .Select(lp => lp.Property?.Name ?? $"Property #{lp.PropertyId}")
                .Distinct()
                .ToList();

            var safeProperties = names.Any()
                ? string.Join(", ", names.Select(WebUtility.HtmlEncode))
                : null;

            var summaryHtml = RichTextRenderer.ToEmailHtml(log.Body);
            var bodyBuilder = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(safeProperties))
            {
                bodyBuilder.AppendLine($@"<p><strong>Properties:</strong> {safeProperties}</p>");
            }

            if (!string.IsNullOrWhiteSpace(summaryHtml))
            {
                bodyBuilder.AppendLine("<p><strong>Summary:</strong></p>");
                bodyBuilder.AppendLine($@"<div style=""font-size:14px;line-height:1.5;word-break:break-word;"">{summaryHtml}</div>");
            }

            var orderedComments = comments?
                .Where(c => c != null)
                .OrderBy(c => c.CreatedAt)
                .ToList() ?? new List<PassOnLogComment>();

            var targetZone = recipientTimeZone ?? TimeZoneInfo.Utc;

            if (orderedComments.Any())
            {
                bodyBuilder.AppendLine(@"<hr style=""margin:24px 0;border:none;border-top:1px solid #e5e7eb;""/>");
                bodyBuilder.AppendLine("<p><strong>Comments</strong></p>");

                foreach (var comment in orderedComments)
                {
                    var authorName = FormatUserName(comment.CreatedBy?.FirstName, comment.CreatedBy?.LastName, comment.CreatedBy?.Email);
                    var safeAuthor = WebUtility.HtmlEncode(authorName);
                    var commentLocal = ConvertToTimeZone(comment.CreatedAt, targetZone);
                    var commentOffset = new DateTimeOffset(commentLocal, targetZone.GetUtcOffset(commentLocal));
                    var timestamp = commentOffset.ToString("MMM d, yyyy h:mm tt zzz");
                    var safeTimestamp = WebUtility.HtmlEncode(timestamp);
                    var commentHtml = RichTextRenderer.ToEmailHtml(comment.Body);
                    if (string.IsNullOrWhiteSpace(commentHtml))
                    {
                        commentHtml = "<p style=\"margin:0;color:#6b7280;\">(No details provided)</p>";
                    }

                    bodyBuilder.AppendLine($@"
<div style=""margin-bottom:16px;padding:12px;border:1px solid #e5e7eb;border-radius:8px;"">
    <div style=""font-size:13px;color:#111827;""><strong>{safeAuthor}</strong><span style=""color:#6b7280;margin-left:8px;"">{safeTimestamp}</span></div>
    <div style=""margin-top:8px;font-size:14px;line-height:1.5;word-break:break-word;"">{commentHtml}</div>
</div>");
                }
            }

            bodyBuilder.AppendLine($@"<p><a href=""{linkUrl}"">Review the log</a></p>");
            return bodyBuilder.ToString();
        }

        private static DateTime ConvertToTimeZone(DateTime utcValue, TimeZoneInfo zone)
        {
            var normalized = utcValue.Kind switch
            {
                DateTimeKind.Unspecified => DateTime.SpecifyKind(utcValue, DateTimeKind.Utc),
                DateTimeKind.Local => utcValue.ToUniversalTime(),
                _ => utcValue
            };
            return TimeZoneInfo.ConvertTimeFromUtc(normalized, zone);
        }
    }
}
