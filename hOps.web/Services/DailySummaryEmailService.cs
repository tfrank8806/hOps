using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using hOps.web.Data;
using hOps.web.Models;
using hOps.web.Utilities;
using hOps.web.ViewModels.WorkOrders;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace hOps.web.Services
{
    public class DailySummaryEmailService : BackgroundService
    {
        private const int PassOnLogPreviewLimit = 260;
        private const int BulletinPreviewLimit = 400;

        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DailySummaryEmailService> _logger;
        private readonly string? _appBaseUrl;
        private static readonly IReadOnlyDictionary<string, string> SalesInquiryLabels =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "group_rooms", "Group Rooms" },
                { "corporate_rate", "Corporate Rate" },
                { "meeting_room", "Meeting Room" },
                { "other", "Other" }
            };

        public DailySummaryEmailService(
            IServiceProvider serviceProvider,
            ILogger<DailySummaryEmailService> logger,
            IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _appBaseUrl = (configuration["App:BaseUrl"] ?? configuration["AppBaseUrl"])?.TrimEnd('/');
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var now = DateTimeOffset.UtcNow;
                var nextRun = GetNextRun(now);
                var delay = nextRun - now;

                if (delay > TimeSpan.Zero)
                {
                    try
                    {
                        await Task.Delay(delay, stoppingToken);
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                }

                if (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    await SendSummariesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to complete daily summary email run.");
                }
            }
        }

        private async Task SendSummariesAsync(CancellationToken cancellationToken)
        {
            var summaryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
            var dayStartUtc = summaryDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var dayEndUtc = dayStartUtc.AddDays(1);

            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

            var users = await context.Users
                .Where(u => u.EmailDailySummary && !string.IsNullOrWhiteSpace(u.Email))
                .Include(u => u.UserPropertyAccesses)
                .Include(u => u.EmailPropertySubscriptions)
                .ToListAsync(cancellationToken);

            foreach (var user in users)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (user.DailySummaryLastSentUtc.HasValue)
                {
                    var lastSentDate = DateOnly.FromDateTime(DateTime.SpecifyKind(user.DailySummaryLastSentUtc.Value, DateTimeKind.Utc));
                    if (lastSentDate >= summaryDate)
                    {
                        continue;
                    }
                }

                var propertyIds = user.EmailPropertySubscriptions?
                    .Where(p => p.IncludeInDailySummary)
                    .Select(p => p.PropertyId)
                    .Distinct()
                    .ToList() ?? new List<int>();

                if (!propertyIds.Any() && user.UserPropertyAccesses != null)
                {
                    propertyIds = user.UserPropertyAccesses
                        .Select(upa => upa.PropertyId)
                        .Distinct()
                        .ToList();
                }

                if (!propertyIds.Any())
                {
                    user.DailySummaryLastSentUtc = dayStartUtc;
                    continue;
                }

                var propertyNameLookup = await LoadPropertyNameLookupAsync(context, propertyIds, cancellationToken);

                var logs = await context.PassOnLogs
                    .AsNoTracking()
                    .Include(l => l.Properties).ThenInclude(lp => lp.Property)
                    .Include(l => l.CreatedBy)
                    .Where(l =>
                        (l.CreatedAt >= dayStartUtc && l.CreatedAt < dayEndUtc) ||
                        (l.UpdatedAt.HasValue && l.UpdatedAt.Value >= dayStartUtc && l.UpdatedAt.Value < dayEndUtc))
                    .Where(l => l.Properties.Any(lp => propertyIds.Contains(lp.PropertyId)))
                    .ToListAsync(cancellationToken);

                var posts = await context.BulletinPosts
                    .AsNoTracking()
                    .Include(p => p.Property)
                    .Include(p => p.CreatedBy)
                    .Where(p => propertyIds.Contains(p.PropertyId))
                    .Where(p =>
                        (p.CreatedAt >= dayStartUtc && p.CreatedAt < dayEndUtc) ||
                        (p.UpdatedAt.HasValue && p.UpdatedAt.Value >= dayStartUtc && p.UpdatedAt.Value < dayEndUtc))
                    .ToListAsync(cancellationToken);

                var salesLeads = await context.SalesLeadSubmissions
                    .AsNoTracking()
                    .Include(sl => sl.Property)
                    .Include(sl => sl.SalesContact)
                    .Where(sl => propertyIds.Contains(sl.PropertyId))
                    .Where(sl => sl.CreatedAtUtc >= dayStartUtc && sl.CreatedAtUtc < dayEndUtc)
                    .ToListAsync(cancellationToken);

                var openWorkOrders = await LoadOpenWorkOrdersAsync(context, propertyIds, cancellationToken);
                var announcements = await LoadAnnouncementsAsync(context, propertyIds, cancellationToken);
                var upcomingEvents = await LoadUpcomingEventsAsync(context, propertyIds, cancellationToken);
                var packageEntries = await LoadOpenPackagesAsync(context, propertyIds, cancellationToken);
                var lostFoundEntries = await LoadOpenLostFoundEntriesAsync(context, propertyIds, cancellationToken);
                var scheduleSummaries = await LoadScheduleSummariesAsync(context, propertyIds, cancellationToken);
                var pmSummary = await LoadPreventiveMaintenanceSummaryAsync(context, propertyIds, dayStartUtc, dayEndUtc, cancellationToken);

                var body = BuildSummaryBody(
                    user,
                    summaryDate,
                    logs,
                    posts,
                    salesLeads,
                    openWorkOrders,
                    packageEntries,
                    lostFoundEntries,
                    upcomingEvents,
                    announcements,
                    scheduleSummaries,
                    propertyIds,
                    propertyNameLookup,
                    _appBaseUrl,
                    pmSummary.Frequencies,
                    pmSummary.Completed,
                    pmSummary.DueSoon,
                    pmSummary.Overdue);
                var subject = $"Daily summary for {summaryDate:MMM d, yyyy}";

                try
                {
                    await emailSender.SendEmailAsync(user.Email!, subject, body);
                    user.DailySummaryLastSentUtc = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to send daily summary email to user {UserId}", user.Id);
                }
            }

            await context.SaveChangesAsync(cancellationToken);
        }

        private static string BuildSummaryBody(
            ApplicationUser user,
            DateOnly summaryDate,
            List<PassOnLog> logs,
            List<BulletinPost> posts,
            List<SalesLeadSubmission> salesLeads,
            IReadOnlyList<DailySummaryWorkOrder> workOrders,
            IReadOnlyList<DailySummaryPackageLog> packageEntries,
            IReadOnlyList<DailySummaryLostFound> lostFoundEntries,
            IReadOnlyList<DailySummaryEvent> upcomingEvents,
            IReadOnlyList<DailySummaryAnnouncement> announcements,
            IReadOnlyList<DailySummarySchedule> schedules,
            IReadOnlyList<int> propertyIds,
            IReadOnlyDictionary<int, string> propertyNames,
            string? baseUrl,
            IReadOnlyDictionary<int, int> pmFrequencies,
            IReadOnlyList<DailySummaryPmSession> completedPms,
            IReadOnlyList<DailySummaryPmDue> duePms,
            IReadOnlyList<DailySummaryPmDue> overduePms)
        {
            var builder = new StringBuilder();
            var userName = BuildUserDisplayName(user);
            var safeName = WebUtility.HtmlEncode(userName);
            var userTimeZone = ResolveUserTimeZone(user);
            var palette = EmailPalette.Default;
            var propertyRecaps = BuildPropertyRecaps(
                propertyIds,
                propertyNames,
                logs,
                posts,
                salesLeads,
                workOrders,
                packageEntries,
                lostFoundEntries,
                upcomingEvents,
                announcements,
                schedules,
                pmFrequencies,
                completedPms,
                duePms,
                overduePms);
            var summaryLabel = summaryDate.ToString("dddd, MMM d", CultureInfo.CurrentCulture);
            var sentLabel = FormatUserLocal(DateTime.UtcNow, userTimeZone, "MMM d, yyyy h:mm tt");
            var heroKpis = BuildHeroKpis();

            builder.AppendLine($@"<div style=""background:{palette.Background};color:{palette.Text};padding:24px;font-family:'Segoe UI','Helvetica Neue',Arial,sans-serif;"">");

            AppendHero();

            if (propertyRecaps.Count == 0)
            {
                builder.AppendLine($@"<p style=""color:{palette.Muted};margin-top:1.5rem;"">No property activity matched your subscriptions for this time range.</p>");
            }
            else
            {
                foreach (var recap in propertyRecaps)
                {
                    AppendPropertyCard(recap);
                }
            }

            AppendFooter();
            builder.AppendLine(@"</div>");

            return builder.ToString();

            List<(string Label, string Value)> BuildHeroKpis()
            {
                return new List<(string Label, string Value)>
                {
                    ("Open WOs", (workOrders?.Count ?? 0).ToString("N0", CultureInfo.CurrentCulture)),
                    ("Packages waiting", (packageEntries?.Count ?? 0).ToString("N0", CultureInfo.CurrentCulture)),
                    ("Lost & Found", (lostFoundEntries?.Count ?? 0).ToString("N0", CultureInfo.CurrentCulture)),
                    ("New Sales Leads", (salesLeads?.Count ?? 0).ToString("N0", CultureInfo.CurrentCulture))
                };
            }

            void AppendHero()
            {
                builder.AppendLine($@"<div style=""background:{palette.HeroBackground};border-radius:24px;border:1px solid {palette.Border};padding:24px;margin-bottom:24px;"">");
                builder.AppendLine($@"<div style=""color:{palette.Muted};font-size:0.9rem;margin:0 0 0.2rem;"">Sent {sentLabel}</div>");
                builder.AppendLine($@"<h1 style=""margin:0;color:{palette.Text};font-size:1.6rem;"">Hello {safeName}, here's your recap for {summaryLabel}.</h1>");
                builder.AppendLine($@"<p style=""margin:0.5rem 0 1rem;color:{palette.Muted};"">The cards below summarize everything logged on your properties during the previous day.</p>");

                var propertyBadges = propertyRecaps
                    .Select(r => WebUtility.HtmlEncode(r.PropertyName))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (propertyBadges.Any())
                {
                    builder.AppendLine(@"<div style=""margin-bottom:1rem;"">");
                    foreach (var badge in propertyBadges)
                    {
                        builder.AppendLine($@"<span style=""display:inline-block;background:{palette.BadgeBackground};color:{palette.AccentAlt};padding:6px 12px;border-radius:999px;margin:0 8px 8px 0;font-size:0.9rem;"">{badge}</span>");
                    }
                    builder.AppendLine(@"</div>");
                }

                builder.AppendLine(@"<table role=""presentation"" width=""100%"" style=""border-collapse:collapse;""><tr>");
                foreach (var kpi in heroKpis)
                {
                    builder.AppendLine($@"<td style=""border:1px solid {palette.Border};border-radius:18px;background:{palette.Panel};padding:14px;text-align:center;font-size:0.9rem;color:{palette.Muted};""><div style=""font-size:1.8rem;font-weight:600;color:{palette.AccentAlt};"">{kpi.Value}</div><div style=""letter-spacing:0.08em;text-transform:uppercase;"">{kpi.Label}</div></td>");
                }
                builder.AppendLine(@"</tr></table>");
                builder.AppendLine(@"</div>");
            }

            void AppendPropertyCard(PropertyRecap recap)
            {
                builder.AppendLine($@"<div style=""background:{palette.Panel};border-radius:24px;border:1px solid {palette.Border};padding:20px;margin-bottom:24px;"">");
                builder.AppendLine(@"<div style=""display:flex;flex-wrap:wrap;justify-content:space-between;gap:12px;margin-bottom:12px;"">");
                builder.AppendLine($@"<div><h2 style=""margin:0;color:{palette.Text};font-size:1.35rem;"">{WebUtility.HtmlEncode(recap.PropertyName)}</h2>");
                var supportLink = BuildAbsoluteUrl($"/Phonebook?propertyId={recap.PropertyId}", baseUrl);
                builder.AppendLine($@"<p style=""margin:0.2rem 0 0;color:{palette.Muted};font-size:0.9rem;"">Need help here? <a href=""{supportLink}"" style=""color:{palette.Accent};"">Open the team directory</a>.</p></div>");
                builder.AppendLine(@"<div style=""display:flex;flex-wrap:wrap;gap:10px;"">");
                foreach (var badge in BuildPropertyBadges(recap))
                {
                    builder.AppendLine($@"<div style=""min-width:120px;background:{palette.BadgeBackground};padding:10px 12px;border-radius:14px;text-align:center;""><div style=""color:{palette.AccentAlt};font-weight:600;font-size:1.2rem;"">{badge.Value}</div><div style=""color:{palette.Muted};font-size:0.8rem;text-transform:uppercase;letter-spacing:0.08em;"">{badge.Label}</div></div>");
                }
                builder.AppendLine(@"</div>");
                builder.AppendLine(@"</div>");

                AppendAnnouncements(recap);
                AppendWorkOrders(recap);
                AppendPreventiveMaintenance(recap);
                AppendPassOnLogs(recap);
                AppendBulletins(recap);
                AppendPackages(recap);
                AppendLostFound(recap);
                AppendEvents(recap);
                AppendSchedules(recap);
                AppendSalesLeads(recap);

                builder.AppendLine(@"</div>");
            }

            IEnumerable<(string Label, string Value)> BuildPropertyBadges(PropertyRecap recap)
            {
                yield return ("Open WOs", recap.WorkOrders.Count.ToString("N0", CultureInfo.CurrentCulture));
                yield return ("Packages", recap.Packages.Count.ToString("N0", CultureInfo.CurrentCulture));
                yield return ("Events", recap.Events.Count.ToString("N0", CultureInfo.CurrentCulture));
                yield return ("Sales Leads", recap.SalesLeads.Count.ToString("N0", CultureInfo.CurrentCulture));
                yield return ("PMs Done", recap.CompletedPms.Count.ToString("N0", CultureInfo.CurrentCulture));
                yield return ("PMs Due", (recap.DuePms.Count + recap.OverduePms.Count).ToString("N0", CultureInfo.CurrentCulture));
            }

            void AppendAnnouncements(PropertyRecap recap)
            {
                AppendSection("Manager Notes &amp; Announcements", recap.Announcements, "No manager notes posted.", announcement =>
                {
                    if (!string.IsNullOrWhiteSpace(announcement.Content))
                    {
                        builder.AppendLine($@"<div style=""margin-bottom:0.6rem;"">{RichTextRenderer.ToHtml(announcement.Content)}</div>");
                    }
                    else
                    {
                        builder.AppendLine($@"<p style=""margin:0 0 0.6rem;color:{palette.Muted};"">No announcement content.</p>");
                    }

                    if (announcement.UpdatedAt.HasValue)
                    {
                        var updated = WebUtility.HtmlEncode(FormatUserLocal(announcement.UpdatedAt.Value, userTimeZone, "MMM d, yyyy h:mm tt"));
                        var updatedBy = string.IsNullOrWhiteSpace(announcement.UpdatedByName)
                            ? string.Empty
                            : $" &middot; {WebUtility.HtmlEncode(announcement.UpdatedByName)}";
                        builder.AppendLine($@"<div style=""color:{palette.Muted};font-size:0.85rem;"">Updated {updated}{updatedBy}</div>");
                    }

                    if (announcement.Attachments.Any())
                    {
                        builder.AppendLine(@"<ul style=""margin:0.4rem 0 0;padding-left:1.25rem;"">");
                        foreach (var attachment in announcement.Attachments)
                        {
                            var link = BuildAbsoluteUrl(attachment.DownloadPath, baseUrl);
                            var fileName = WebUtility.HtmlEncode(attachment.FileName);
                            builder.AppendLine($@"<li><a href=""{link}"" style=""color:{palette.Accent};"">{fileName}</a></li>");
                        }
                        builder.AppendLine(@"</ul>");
                    }
                });
            }

            void AppendWorkOrders(PropertyRecap recap)
            {
                var ordered = recap.WorkOrders.OrderBy(o => o.DueDate).ToList();
                AppendSection("Open Work Orders", ordered, "All work orders are complete.", order =>
                {
                    var issue = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(order.Issue) ? "Work Order" : order.Issue);
                    var statusLabel = WebUtility.HtmlEncode(WorkOrderStatusOptions.GetLabel(order.Status));
                    var openedAt = WebUtility.HtmlEncode(FormatUserLocal(order.CreatedAt, userTimeZone, "MMM d, yyyy h:mm tt"));
                    var dueAt = WebUtility.HtmlEncode(FormatUserLocal(order.DueDate, userTimeZone, "MMM d, yyyy h:mm tt"));
                    var orderLink = BuildAbsoluteUrl(order.DetailPath, baseUrl);

                    builder.AppendLine($@"<div style=""font-weight:600;"">{issue}</div>");
                    builder.AppendLine($@"<div style=""color:{palette.Muted};font-size:0.9rem;margin:0.2rem 0;"">Status: <span style=""color:{palette.AccentAlt};"">{statusLabel}</span> &middot; Due {dueAt}</div>");

                    if (!string.IsNullOrWhiteSpace(order.Location))
                    {
                        builder.AppendLine($@"<div style=""color:{palette.Muted};font-size:0.9rem;"">Location: {WebUtility.HtmlEncode(order.Location)}</div>");
                    }
                    if (!string.IsNullOrWhiteSpace(order.DepartmentName))
                    {
                        builder.AppendLine($@"<div style=""color:{palette.Muted};font-size:0.9rem;"">Department: {WebUtility.HtmlEncode(order.DepartmentName)}</div>");
                    }

                    var sharedWith = order.PropertyNames
                        .Where(name => !string.Equals(name, recap.PropertyName, StringComparison.OrdinalIgnoreCase))
                        .Select(WebUtility.HtmlEncode)
                        .ToList();
                    if (sharedWith.Any())
                    {
                        builder.AppendLine($@"<div style=""color:{palette.Muted};font-size:0.85rem;margin-top:0.2rem;"">Shared with: {string.Join("", "", sharedWith)}</div>");
                    }

                    builder.AppendLine($@"<div style=""color:{palette.Muted};font-size:0.85rem;margin-top:0.2rem;"">Opened {openedAt}</div>");
                    builder.AppendLine($@"<a href=""{orderLink}"" style=""color:{palette.Accent};font-weight:600;text-decoration:none;"">View work order &rarr;</a>");
                });
            }

            void AppendPreventiveMaintenance(PropertyRecap recap)
            {
                if (!recap.CompletedPms.Any() && !recap.DuePms.Any() && !recap.OverduePms.Any())
                {
                    return;
                }

                builder.AppendLine($@"<div style=""margin-top:1.5rem;"">");
                builder.AppendLine($@"<h3 style=""margin:0 0 0.3rem;color:{palette.AccentAlt};font-size:1.05rem;"">Preventative Maintenance</h3>");

                if (recap.CompletedPms.Any())
                {
                    builder.AppendLine($@"<p style=""margin:0 0 0.3rem;color:{palette.Muted};"">Completed yesterday:</p>");
                    builder.AppendLine(@"<ul style=""margin:0 0 1rem;padding-left:1.2rem;"">");
                    foreach (var entry in recap.CompletedPms.OrderByDescending(p => p.CompletedAtUtc).Take(5))
                    {
                        var completedLabel = WebUtility.HtmlEncode(FormatUserLocal(entry.CompletedAtUtc, userTimeZone, "MMM d, h:mm tt"));
                        builder.AppendLine($@"<li><strong>{WebUtility.HtmlEncode(entry.RoomNumber)}</strong> &middot; {completedLabel} ({FormatPmDuration(entry.DurationSeconds)})</li>");
                    }
                    builder.AppendLine(@"</ul>");
                }
                else
                {
                    builder.AppendLine($@"<p style=""margin:0 0 1rem;color:{palette.Muted};"">No PMs were completed yesterday.</p>");
                }

                var dueEntries = recap.OverduePms.Concat(recap.DuePms).OrderBy(d => d.DueAtUtc).ToList();
                if (dueEntries.Any())
                {
                    builder.AppendLine($@"<p style=""margin:0 0 0.3rem;color:{palette.Muted};"">Due soon or overdue:</p>");
                    builder.AppendLine(@"<ul style=""margin:0;padding-left:1.2rem;"">");
                    foreach (var entry in dueEntries.Take(5))
                    {
                        var dueLabel = WebUtility.HtmlEncode(FormatUserLocal(entry.DueAtUtc, userTimeZone, "MMM d"));
                        builder.AppendLine($@"<li><strong>{WebUtility.HtmlEncode(entry.RoomNumber)}</strong> &middot; due {dueLabel}</li>");
                    }
                    builder.AppendLine(@"</ul>");
                }
                else
                {
                    builder.AppendLine($@"<p style=""margin:0;color:{palette.Muted};"">All rooms are up to date.</p>");
                }

                builder.AppendLine(@"</div>");

                static string FormatPmDuration(double seconds)
                {
                    var span = TimeSpan.FromSeconds(Math.Max(0, seconds));
                    if (span.TotalHours >= 1)
                    {
                        return $"{(int)span.TotalHours}h {span.Minutes}m";
                    }

                    if (span.TotalMinutes >= 1)
                    {
                        return $"{(int)span.TotalMinutes}m {span.Seconds}s";
                    }

                    return $"{span.Seconds}s";
                }
            }

            void AppendPassOnLogs(PropertyRecap recap)
            {
                var ordered = recap.PassOnLogs.OrderByDescending(l => l.CreatedAt).ToList();
                AppendSection("Pass On Logs (24h)", ordered, "No pass on logs were posted.", log =>
                {
                    var logTitle = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(log.Title) ? "Pass On Log" : log.Title);
                    var createdAt = WebUtility.HtmlEncode(FormatUserLocal(log.CreatedAt, userTimeZone, "MMM d, yyyy h:mm tt"));
                    var previewHtml = BuildRichTextPreview(log.Body, PassOnLogPreviewLimit);
                    var link = BuildAbsoluteUrl($"/PassOnLogs/Details/{log.Id}", baseUrl);

                    builder.AppendLine($@"<div style=""font-weight:600;"">{logTitle}</div>");
                    builder.AppendLine($@"<div style=""color:{palette.Muted};font-size:0.9rem;margin-bottom:0.4rem;"">{createdAt}</div>");
                    if (!string.IsNullOrEmpty(previewHtml))
                    {
                        builder.AppendLine($@"<div style=""margin-bottom:0.4rem;"">{previewHtml}</div>");
                    }
                    builder.AppendLine($@"<a href=""{link}"" style=""color:{palette.Accent};font-weight:600;text-decoration:none;"">Read full log &rarr;</a>");
                });
            }

            void AppendBulletins(PropertyRecap recap)
            {
                var ordered = recap.Bulletins.OrderByDescending(b => b.CreatedAt).ToList();
                AppendSection("Bulletin Board", ordered, "No bulletin posts were added yesterday.", post =>
                {
                    var propertyLink = BuildAbsoluteUrl($"/Home?propertyId={post.PropertyId}#bulletin-board", baseUrl);
                    var createdAt = WebUtility.HtmlEncode(FormatUserLocal(post.CreatedAt, userTimeZone, "MMM d, yyyy h:mm tt"));
                    var contentHtml = BuildRichTextPreview(post.Content, BulletinPreviewLimit);

                    builder.AppendLine($@"<div style=""font-weight:600;"">{WebUtility.HtmlEncode(post.Property?.Name ?? recap.PropertyName)}</div>");
                    builder.AppendLine($@"<div style=""color:{palette.Muted};font-size:0.9rem;margin-bottom:0.4rem;"">{createdAt}</div>");
                    if (!string.IsNullOrEmpty(contentHtml))
                    {
                        builder.AppendLine($@"<div style=""margin-bottom:0.4rem;"">{contentHtml}</div>");
                    }
                    builder.AppendLine($@"<a href=""{propertyLink}"" style=""color:{palette.Accent};font-weight:600;text-decoration:none;"">Open bulletin &rarr;</a>");
                });
            }

            void AppendPackages(PropertyRecap recap)
            {
                var ordered = recap.Packages.OrderByDescending(p => p.PackageReceivedDate ?? p.LoggedAt).ToList();
                AppendSection("Open Package Log", ordered, "No open packages are waiting.", entry =>
                {
                    var recipient = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(entry.RecipientName) ? "Package" : entry.RecipientName);
                    var receivedSource = entry.PackageReceivedDate ?? entry.LoggedAt;
                    var received = WebUtility.HtmlEncode(FormatUserLocal(receivedSource, userTimeZone, "MMM d, yyyy h:mm tt"));
                    var loggedLabel = WebUtility.HtmlEncode(FormatUserLocal(entry.LoggedAt, userTimeZone, "MMM d, yyyy h:mm tt"));
                    var room = string.IsNullOrWhiteSpace(entry.RoomNumber) ? null : WebUtility.HtmlEncode(entry.RoomNumber);
                    var carrier = string.IsNullOrWhiteSpace(entry.Carrier) ? null : WebUtility.HtmlEncode(entry.Carrier);
                    var tracking = string.IsNullOrWhiteSpace(entry.TrackingNumber) ? null : WebUtility.HtmlEncode(entry.TrackingNumber);
                    var storage = string.IsNullOrWhiteSpace(entry.StorageLocation) ? null : WebUtility.HtmlEncode(entry.StorageLocation);
                    var link = BuildAbsoluteUrl(entry.DetailPath, baseUrl);
                    var waitingDays = Math.Max(0, (int)System.Math.Floor((DateTime.UtcNow - DateTime.SpecifyKind(receivedSource, DateTimeKind.Utc)).TotalDays));
                    var agingChip = waitingDays >= 2
                        ? $@"<span style=""display:inline-block;margin-left:0.4rem;padding:2px 8px;border-radius:999px;background:{palette.Accent};color:#03121f;font-size:0.75rem;font-weight:600;"">{waitingDays}+ days</span>"
                        : string.Empty;

                    builder.AppendLine($@"<div style=""font-weight:600;"">{recipient}{agingChip}</div>");
                    builder.AppendLine($@"<div style=""color:{palette.Muted};font-size:0.9rem;margin:0.2rem 0;"">Received {received}<br/>Logged {loggedLabel}</div>");

                    if (room != null)
                    {
                        builder.AppendLine($@"<div style=""color:{palette.Muted};font-size:0.9rem;"">Room: {room}</div>");
                    }
                    if (carrier != null)
                    {
                        builder.AppendLine($@"<div style=""color:{palette.Muted};font-size:0.9rem;"">Carrier: {carrier}</div>");
                    }
                    if (tracking != null)
                    {
                        builder.AppendLine($@"<div style=""color:{palette.Muted};font-size:0.9rem;"">Tracking: {tracking}</div>");
                    }
                    if (storage != null)
                    {
                        builder.AppendLine($@"<div style=""color:{palette.Muted};font-size:0.9rem;"">Storage: {storage}</div>");
                    }

                    builder.AppendLine($@"<a href=""{link}"" style=""color:{palette.Accent};font-weight:600;text-decoration:none;"">View package entry &rarr;</a>");
                });
            }

            void AppendLostFound(PropertyRecap recap)
            {
                var ordered = recap.LostFoundEntries.OrderByDescending(l => l.CreatedAt).ToList();
                AppendSection("Open Lost &amp; Found", ordered, "No open lost &amp; found entries.", entry =>
                {
                    var title = WebUtility.HtmlEncode(entry.Title);
                    var status = WebUtility.HtmlEncode(entry.Status);
                    var type = string.IsNullOrWhiteSpace(entry.Type) ? null : WebUtility.HtmlEncode(entry.Type);
                    var createdAt = WebUtility.HtmlEncode(FormatUserLocal(entry.CreatedAt, userTimeZone, "MMM d, yyyy h:mm tt"));
                    var link = BuildAbsoluteUrl(entry.DetailPath, baseUrl);

                    builder.AppendLine($@"<div style=""font-weight:600;"">{title}</div>");
                    builder.AppendLine($@"<div style=""color:{palette.Muted};font-size:0.9rem;margin:0.2rem 0;"">Status: {status}</div>");
                    if (type != null)
                    {
                        builder.AppendLine($@"<div style=""color:{palette.Muted};font-size:0.9rem;"">Type: {type}</div>");
                    }
                    builder.AppendLine($@"<div style=""color:{palette.Muted};font-size:0.85rem;margin-bottom:0.2rem;"">Logged {createdAt}</div>");
                    builder.AppendLine($@"<a href=""{link}"" style=""color:{palette.Accent};font-weight:600;text-decoration:none;"">View entry &rarr;</a>");
                });
            }

            void AppendEvents(PropertyRecap recap)
            {
                var ordered = recap.Events.OrderBy(e => e.StartDate).ToList();
                AppendSection("Upcoming Events", ordered, "No upcoming events have been posted.", calendarEvent =>
                {
                    var title = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(calendarEvent.Title) ? "Event" : calendarEvent.Title);
                    var dateLabel = WebUtility.HtmlEncode(BuildEventDateLabel(calendarEvent));
                    var timeLabel = BuildEventTimeLabel(calendarEvent);
                    var link = BuildAbsoluteUrl(calendarEvent.DetailPath, baseUrl);

                    builder.AppendLine($@"<div style=""font-weight:600;"">{title}</div>");
                    builder.AppendLine($@"<div style=""color:{palette.Muted};font-size:0.9rem;"">{dateLabel}");
                    if (!string.IsNullOrEmpty(timeLabel))
                    {
                        builder.AppendLine($@" &middot; {WebUtility.HtmlEncode(timeLabel)}");
                    }
                    builder.AppendLine(@"</div>");
                    builder.AppendLine($@"<a href=""{link}"" style=""color:{palette.Accent};font-weight:600;text-decoration:none;"">View calendar &rarr;</a>");
                });
            }

            void AppendSchedules(PropertyRecap recap)
            {
                if (!recap.CurrentSchedules.Any() && !recap.UpcomingSchedules.Any())
                {
                    AppendSection("Staff Schedules", Array.Empty<DailySummarySchedule>(), "No posted schedules for the current or upcoming week.", _ => { });
                    return;
                }

                builder.AppendLine($@"<div style=""margin-top:18px;""><h3 style=""margin:0 0 6px;color:{palette.Accent};font-size:1rem;text-transform:uppercase;letter-spacing:0.08em;"">Staff Schedules</h3>");

                RenderScheduleGroup("Current Week", recap.CurrentSchedules.OrderBy(s => s.WeekStart));
                RenderScheduleGroup("Upcoming Week", recap.UpcomingSchedules.OrderBy(s => s.WeekStart));

                builder.AppendLine(@"</div>");
            }

            void RenderScheduleGroup(string title, IEnumerable<DailySummarySchedule> scheduleList)
            {
                var schedulesArray = scheduleList.ToList();
                if (!schedulesArray.Any())
                {
                    builder.AppendLine($@"<p style=""color:{palette.Muted};margin:0 0 0.4rem;"">{title}: no posted schedule.</p>");
                    return;
                }

                builder.AppendLine($@"<p style=""color:{palette.Muted};margin:0 0 0.4rem;font-weight:600;"">{title}</p>");
                builder.AppendLine(@"<ul style=""list-style:none;padding:0;margin:0;"">");
                foreach (var schedule in schedulesArray)
                {
                    var weekRange = $"{FormatUserLocal(schedule.WeekStart, userTimeZone, "MMM d")} - {FormatUserLocal(schedule.WeekEnd, userTimeZone, "MMM d")}";
                    var link = BuildAbsoluteUrl(schedule.DetailPath, baseUrl);

                    builder.AppendLine($@"<li style=""background:{palette.ListItemBackground};border-radius:12px;padding:10px 12px;border:1px solid {palette.Border};margin-bottom:10px;""><div style=""font-weight:600;"">{WebUtility.HtmlEncode(schedule.Title)}</div><div style=""color:{palette.Muted};font-size:0.9rem;margin:0.2rem 0;"">{weekRange}</div><a href=""{link}"" style=""color:{palette.Accent};font-weight:600;text-decoration:none;"">View schedule &rarr;</a></li>");
                }
                builder.AppendLine(@"</ul>");
            }

            void AppendSalesLeads(PropertyRecap recap)
            {
                var ordered = recap.SalesLeads.OrderByDescending(l => l.CreatedAtUtc).ToList();
                AppendSection("Sales Leads (24h)", ordered, "No new sales leads were submitted.", lead =>
                {
                    var submittedAt = WebUtility.HtmlEncode(FormatUserLocal(lead.CreatedAtUtc, userTimeZone, "MMM d, yyyy h:mm tt"));
                    var submittedBy = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(lead.SubmittedByName) ? "Team Member" : lead.SubmittedByName);
                    var groupName = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(lead.GroupName) ? "N/A" : lead.GroupName);
                    var contactName = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(lead.ContactName) ? "Not provided" : lead.ContactName);
                    var contactEmail = string.IsNullOrWhiteSpace(lead.ContactEmail) ? "Not provided" : lead.ContactEmail!;
                    var contactPhone = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(lead.ContactPhone) ? "Not provided" : lead.ContactPhone!);
                    var inquiryHtml = BuildInquiryListHtml(lead);
                    var datesText = BuildDateRangeDescription(lead.EventStartDate, lead.EventEndDate, userTimeZone);
                    var budgetText = BuildBudgetDescription(lead.BudgetMinimum, lead.BudgetMaximum);
                    var detailsHtml = BuildPlainTextHtml(lead.AdditionalDetails);
                    var salesLink = BuildAbsoluteUrl("/Sales", baseUrl);

                    builder.AppendLine($@"<div style=""font-weight:600;"">{groupName}</div>");
                    builder.AppendLine($@"<div style=""color:{palette.Muted};font-size:0.9rem;margin-bottom:0.2rem;"">Submitted {submittedAt} by {submittedBy}</div>");
                    builder.AppendLine($@"<div style=""color:{palette.Muted};font-size:0.9rem;margin-bottom:0.3rem;"">Contact: {contactName} &middot; <a href=""mailto:{WebUtility.HtmlEncode(contactEmail)}"" style=""color:{palette.Accent};"">{WebUtility.HtmlEncode(contactEmail)}</a> &middot; {contactPhone}</div>");

                    if (!string.IsNullOrEmpty(inquiryHtml))
                    {
                        builder.AppendLine($@"<div style=""margin-bottom:0.3rem;""><strong>Inquiry:</strong><br/>{inquiryHtml}</div>");
                    }

                    builder.AppendLine($@"<div style=""margin-bottom:0.3rem;""><strong>Dates:</strong> {WebUtility.HtmlEncode(datesText)}<br/><strong>Budget:</strong> {WebUtility.HtmlEncode(budgetText)}</div>");

                    if (!string.IsNullOrEmpty(detailsHtml))
                    {
                        builder.AppendLine($@"<div style=""margin-bottom:0.3rem;""><strong>Additional details:</strong><br/>{detailsHtml}</div>");
                    }

                    builder.AppendLine($@"<a href=""{salesLink}"" style=""color:{palette.Accent};font-weight:600;text-decoration:none;"">Open sales workspace &rarr;</a>");
                });
            }

            void AppendSection<T>(string title, IReadOnlyCollection<T> items, string emptyMessage, Action<T> renderItem)
            {
                builder.AppendLine($@"<div style=""margin-top:18px;""><h3 style=""margin:0 0 6px;color:{palette.Accent};font-size:1rem;text-transform:uppercase;letter-spacing:0.08em;"">{title}</h3>");
                if (items == null || items.Count == 0)
                {
                    builder.AppendLine($@"<p style=""color:{palette.Muted};margin:0;"">{emptyMessage}</p></div>");
                    return;
                }

                foreach (var item in items)
                {
                    builder.AppendLine($@"<div style=""background:{palette.ListItemBackground};border-radius:14px;padding:12px 14px;border:1px solid {palette.Border};margin-bottom:10px;"">");
                    renderItem(item);
                    builder.AppendLine(@"</div>");
                }

                builder.AppendLine(@"</div>");
            }

            void AppendFooter()
            {
                var manageLink = BuildAbsoluteUrl("/Identity/Account/Manage#notifications", baseUrl);
                builder.AppendLine($@"<div style=""margin-top:28px;padding:18px;border-radius:18px;border:1px solid {palette.Border};background:{palette.HeroBackground};text-align:center;""><p style=""margin:0 0 0.6rem;color:{palette.Muted};"">You are receiving this recap because daily summaries are enabled in your profile preferences.</p><a href=""{manageLink}"" style=""display:inline-block;padding:10px 22px;border-radius:999px;background:{palette.Accent};color:#02111d;font-weight:600;text-decoration:none;"">Manage notification settings</a><p style=""margin:0.75rem 0 0;color:{palette.Muted};font-size:0.85rem;"">Need to escalate anything? Reply to this email or use the property directory links above to reach your leadership team.</p></div>");
            }
        }


        private static async Task<List<DailySummaryWorkOrder>> LoadOpenWorkOrdersAsync(
            ApplicationDbContext context,
            List<int> propertyIds,
            CancellationToken cancellationToken)
        {
            if (propertyIds == null || propertyIds.Count == 0)
            {
                return new List<DailySummaryWorkOrder>();
            }

            var openStatuses = new[] { "New", "In Progress", "Escalated", "On Hold" };

            var workOrders = await context.WorkOrders
                .AsNoTracking()
                .Include(wo => wo.Properties)
                    .ThenInclude(wp => wp.Property)
                .Include(wo => wo.Department)
                .Where(wo => openStatuses.Contains(wo.Status))
                .Where(wo => wo.Properties.Any(p => propertyIds.Contains(p.PropertyId)))
                .OrderBy(wo => wo.DueDate)
                .ToListAsync(cancellationToken);

            return workOrders.Select(wo => new DailySummaryWorkOrder
            {
                Id = wo.Id,
                Issue = wo.Issue ?? "Work Order",
                Status = wo.Status ?? string.Empty,
                Location = wo.Location,
                DepartmentName = wo.Department?.Name,
                PropertyNames = wo.Properties
                    .Where(p => propertyIds.Contains(p.PropertyId))
                    .Select(p => string.IsNullOrWhiteSpace(p.Property?.Name) ? $"Property #{p.PropertyId}" : p.Property!.Name!)
                    .Distinct()
                    .ToList(),
                PropertyIds = wo.Properties
                    .Where(p => propertyIds.Contains(p.PropertyId))
                    .Select(p => p.PropertyId)
                    .Distinct()
                    .ToList(),
                CreatedAt = wo.CreatedAt,
                DueDate = wo.DueDate,
                DetailPath = $"/WorkOrders/Edit/{wo.Id}"
            }).ToList();
        }

        private static async Task<Dictionary<int, string>> LoadPropertyNameLookupAsync(
            ApplicationDbContext context,
            IReadOnlyCollection<int> propertyIds,
            CancellationToken cancellationToken)
        {
            if (propertyIds == null || propertyIds.Count == 0)
            {
                return new Dictionary<int, string>();
            }

            return await context.Properties
                .AsNoTracking()
                .Where(p => propertyIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);
        }

        private static async Task<List<DailySummaryAnnouncement>> LoadAnnouncementsAsync(
            ApplicationDbContext context,
            List<int> propertyIds,
            CancellationToken cancellationToken)
        {
            if (propertyIds == null || propertyIds.Count == 0)
            {
                return new List<DailySummaryAnnouncement>();
            }

            var announcements = await context.ManagerAnnouncements
                .AsNoTracking()
                .Include(a => a.Property)
                .Include(a => a.Attachments)
                .Include(a => a.UpdatedBy)
                .Where(a => propertyIds.Contains(a.PropertyId))
                .ToListAsync(cancellationToken);

            return announcements
                .GroupBy(a => a.PropertyId)
                .Select(group => group
                    .OrderByDescending(a => a.UpdatedAt)
                    .First())
                .Select(announcement => new DailySummaryAnnouncement
                {
                    PropertyId = announcement.PropertyId,
                    PropertyName = string.IsNullOrWhiteSpace(announcement.Property?.Name)
                        ? $"Property #{announcement.PropertyId}"
                        : announcement.Property!.Name!,
                    Content = announcement.Content,
                    UpdatedAt = announcement.UpdatedAt,
                    UpdatedByName = BuildUserDisplayName(announcement.UpdatedBy),
                    Attachments = announcement.Attachments
                        .OrderBy(a => string.IsNullOrWhiteSpace(a.OriginalFileName) ? a.FilePath : a.OriginalFileName, StringComparer.OrdinalIgnoreCase)
                        .Select(a => new DailySummaryAttachment
                        {
                            FileName = string.IsNullOrWhiteSpace(a.OriginalFileName) ? a.FilePath ?? "Attachment" : a.OriginalFileName!,
                            DownloadPath = a.FilePath ?? string.Empty
                        })
                        .ToList()
                })
                .ToList();
        }

        private static async Task<List<DailySummaryEvent>> LoadUpcomingEventsAsync(
            ApplicationDbContext context,
            List<int> propertyIds,
            CancellationToken cancellationToken)
        {
            if (propertyIds == null || propertyIds.Count == 0)
            {
                return new List<DailySummaryEvent>();
            }

            var today = DateTime.UtcNow.Date;
            var events = await context.CalendarEvents
                .AsNoTracking()
                .Include(e => e.Category)
                .Include(e => e.EventProperties)
                    .ThenInclude(ep => ep.Property)
                .Where(e => e.EventProperties.Any(ep => propertyIds.Contains(ep.PropertyId)))
                .Where(e => e.EndDate >= today)
                .OrderBy(e => e.StartDate)
                .Take(5)
                .ToListAsync(cancellationToken);

            return events.Select(e => new DailySummaryEvent
            {
                Title = e.Title,
                StartDate = e.StartDate,
                EndDate = e.EndDate,
                StartTime = e.StartTime,
                EndTime = e.EndTime,
                CategoryName = e.Category?.Name ?? "Event",
                PropertyNames = e.EventProperties
                    .Where(ep => propertyIds.Contains(ep.PropertyId))
                    .Select(ep => string.IsNullOrWhiteSpace(ep.Property?.Name) ? $"Property #{ep.PropertyId}" : ep.Property!.Name!)
                    .Distinct()
                    .ToList(),
                PropertyIds = e.EventProperties
                    .Where(ep => propertyIds.Contains(ep.PropertyId))
                    .Select(ep => ep.PropertyId)
                    .Distinct()
                    .ToList(),
                DetailPath = "/Calendar"
            }).ToList();
        }

        private static async Task<List<DailySummaryPackageLog>> LoadOpenPackagesAsync(
            ApplicationDbContext context,
            List<int> propertyIds,
            CancellationToken cancellationToken)
        {
            if (propertyIds == null || propertyIds.Count == 0)
            {
                return new List<DailySummaryPackageLog>();
            }

            var packages = await context.PackageLogEntries
                .AsNoTracking()
                .Include(p => p.Property)
                .Where(p => propertyIds.Contains(p.PropertyId) && !p.Delivered)
                .OrderByDescending(p => p.LoggedAt)
                .ToListAsync(cancellationToken);

            return packages.Select(p => new DailySummaryPackageLog
            {
                Id = p.Id,
                PropertyId = p.PropertyId,
                PropertyName = string.IsNullOrWhiteSpace(p.Property?.Name) ? $"Property #{p.PropertyId}" : p.Property!.Name!,
                RecipientName = p.RecipientName,
                RoomNumber = p.RoomNumber,
                Carrier = p.Carrier,
                TrackingNumber = p.TrackingNumber,
                StorageLocation = p.StorageLocation,
                PackageReceivedDate = p.PackageReceivedDate,
                LoggedAt = p.LoggedAt,
                DetailPath = $"/MailLog/Details/{p.Id}"
            }).ToList();
        }

        private static async Task<List<DailySummaryLostFound>> LoadOpenLostFoundEntriesAsync(
            ApplicationDbContext context,
            List<int> propertyIds,
            CancellationToken cancellationToken)
        {
            if (propertyIds == null || propertyIds.Count == 0)
            {
                return new List<DailySummaryLostFound>();
            }

            var lostFoundEntries = await context.LostFoundEntries
                .AsNoTracking()
                .Include(lf => lf.Property)
                .Where(lf =>
                    propertyIds.Contains(lf.PropertyId) &&
                    lf.Status != LostFoundStatus.ReturnedToGuest &&
                    lf.Status != LostFoundStatus.DisposedOf)
                .OrderByDescending(lf => lf.CreatedAt)
                .ToListAsync(cancellationToken);

            return lostFoundEntries.Select(lf => new DailySummaryLostFound
            {
                Id = lf.Id,
                PropertyId = lf.PropertyId,
                Title = !string.IsNullOrWhiteSpace(lf.ItemFound)
                    ? lf.ItemFound!
                    : (!string.IsNullOrWhiteSpace(lf.ItemLost) ? lf.ItemLost! : "Lost & Found Entry"),
                PropertyName = string.IsNullOrWhiteSpace(lf.Property?.Name) ? $"Property #{lf.PropertyId}" : lf.Property!.Name!,
                Status = lf.Status.ToString(),
                Type = lf.Type.ToString(),
                CreatedAt = lf.CreatedAt,
                DetailPath = $"/LostAndFound/Details/{lf.Id}"
            }).ToList();
        }

        private static async Task<List<DailySummarySchedule>> LoadScheduleSummariesAsync(
            ApplicationDbContext context,
            List<int> propertyIds,
            CancellationToken cancellationToken)
        {
            var summaries = new List<DailySummarySchedule>();
            if (propertyIds == null || propertyIds.Count == 0)
            {
                return summaries;
            }

            var settings = await context.ScheduleSettings
                .AsNoTracking()
                .Where(s => propertyIds.Contains(s.PropertyId))
                .ToDictionaryAsync(s => s.PropertyId, s => s.StartDayOfWeek, cancellationToken);

            var today = DateTime.UtcNow.Date;
            var targetWeeks = new Dictionary<(int PropertyId, DateTime WeekStart), bool>();

            foreach (var propertyId in propertyIds)
            {
                var startDay = settings.TryGetValue(propertyId, out var configuredStart) ? configuredStart : DayOfWeek.Monday;
                var currentStart = AlignToWeekStart(today, startDay);
                targetWeeks[(propertyId, currentStart)] = false;
                targetWeeks[(propertyId, currentStart.AddDays(7))] = true;
            }

            var weekValues = targetWeeks.Keys.Select(k => k.WeekStart).Distinct().ToList();

            var schedules = await context.Schedules
                .AsNoTracking()
                .Include(s => s.Property)
                .Where(s => propertyIds.Contains(s.PropertyId) &&
                            s.Status == ScheduleStatus.Posted &&
                            weekValues.Contains(s.WeekStartDate))
                .ToListAsync(cancellationToken);

            foreach (var schedule in schedules)
            {
                var key = (schedule.PropertyId, schedule.WeekStartDate);
                var isUpcoming = targetWeeks.TryGetValue(key, out var upcoming) ? upcoming : schedule.WeekStartDate > today;

                summaries.Add(new DailySummarySchedule
                {
                    PropertyId = schedule.PropertyId,
                    PropertyName = string.IsNullOrWhiteSpace(schedule.Property?.Name) ? $"Property #{schedule.PropertyId}" : schedule.Property!.Name!,
                    Title = string.IsNullOrWhiteSpace(schedule.Title) ? "Weekly Schedule" : schedule.Title!,
                    WeekStart = schedule.WeekStartDate,
                    WeekEnd = schedule.WeekEndDate,
                    DetailPath = $"/Schedules?weekStart={schedule.WeekStartDate:yyyy-MM-dd}",
                    IsUpcoming = isUpcoming
                });
            }

            return summaries;
        }

        private static async Task<PreventiveMaintenanceSummaryData> LoadPreventiveMaintenanceSummaryAsync(
            ApplicationDbContext context,
            List<int> propertyIds,
            DateTime dayStartUtc,
            DateTime dayEndUtc,
            CancellationToken cancellationToken)
        {
            if (propertyIds == null || propertyIds.Count == 0)
            {
                return new PreventiveMaintenanceSummaryData();
            }

            var propertySet = new HashSet<int>(propertyIds);

            var completed = await context.PreventiveMaintenanceSessions
                .AsNoTracking()
                .Where(s => propertySet.Contains(s.PropertyId) && s.Status == PreventiveMaintenanceSessionStatus.Completed)
                .Where(s => s.CompletedAtUtc >= dayStartUtc && s.CompletedAtUtc < dayEndUtc)
                .Select(s => new DailySummaryPmSession
                {
                    PropertyId = s.PropertyId,
                    RoomNumber = s.RoomNumber,
                    CompletedAtUtc = s.CompletedAtUtc ?? s.StartedAtUtc,
                    DurationSeconds = s.TotalDurationSeconds
                })
                .ToListAsync(cancellationToken);

            var frequencies = await context.PreventiveMaintenanceSettings
                .AsNoTracking()
                .Where(s => propertySet.Contains(s.PropertyId))
                .ToDictionaryAsync(s => s.PropertyId, s => s.FrequencyPerYear, cancellationToken);

            if (frequencies.Count == 0)
            {
                return new PreventiveMaintenanceSummaryData
                {
                    Completed = completed,
                    Frequencies = new Dictionary<int, int>()
                };
            }

            var rooms = await context.Rooms
                .AsNoTracking()
                .Where(r => propertySet.Contains(r.PropertyId))
                .Select(r => new { r.Id, r.PropertyId, r.RoomNumber })
                .ToListAsync(cancellationToken);

            var lookbackStart = dayEndUtc.AddMonths(-18);
            var recentSessions = await context.PreventiveMaintenanceSessions
                .AsNoTracking()
                .Where(s => propertySet.Contains(s.PropertyId) && s.Status == PreventiveMaintenanceSessionStatus.Completed)
                .Where(s => s.CompletedAtUtc >= lookbackStart)
                .OrderByDescending(s => s.CompletedAtUtc)
                .Select(s => new
                {
                    s.PropertyId,
                    s.RoomId,
                    s.RoomNumber,
                    CompletedAtUtc = s.CompletedAtUtc ?? s.StartedAtUtc
                })
                .ToListAsync(cancellationToken);

            var latestByRoom = new Dictionary<(int PropertyId, int RoomId), DateTime>();
            foreach (var session in recentSessions)
            {
                if (!session.RoomId.HasValue)
                {
                    continue;
                }

                var key = (session.PropertyId, session.RoomId.Value);
                if (!latestByRoom.ContainsKey(key))
                {
                    latestByRoom[key] = session.CompletedAtUtc;
                }
            }

            var dueSoon = new List<DailySummaryPmDue>();
            var overdue = new List<DailySummaryPmDue>();
            var dueCutoff = dayEndUtc.AddDays(7);

            foreach (var kvp in frequencies)
            {
                var propertyId = kvp.Key;
                var frequency = kvp.Value;
                if (frequency <= 0)
                {
                    continue;
                }

                var intervalDays = Math.Max(1, 365.0 / frequency);
                var propertyRooms = rooms.Where(r => r.PropertyId == propertyId).ToList();
                foreach (var room in propertyRooms)
                {
                    var key = (propertyId, room.Id);
                    var hasLast = latestByRoom.TryGetValue(key, out var lastCompleted);
                    var dueAt = hasLast ? lastCompleted.AddDays(intervalDays) : dayStartUtc;
                    var label = string.IsNullOrWhiteSpace(room.RoomNumber) ? $"Room {room.Id}" : room.RoomNumber!;

                    if (!hasLast || dueAt < dayStartUtc)
                    {
                        overdue.Add(new DailySummaryPmDue
                        {
                            PropertyId = propertyId,
                            RoomNumber = label,
                            DueAtUtc = dueAt
                        });
                    }
                    else if (dueAt <= dueCutoff)
                    {
                        dueSoon.Add(new DailySummaryPmDue
                        {
                            PropertyId = propertyId,
                            RoomNumber = label,
                            DueAtUtc = dueAt
                        });
                    }
                }
            }

            return new PreventiveMaintenanceSummaryData
            {
                Completed = completed,
                DueSoon = dueSoon,
                Overdue = overdue,
                Frequencies = frequencies
            };
        }

        private static string BuildEventDateLabel(DailySummaryEvent calendarEvent)
        {
            return calendarEvent.StartDate.Date == calendarEvent.EndDate.Date
                ? calendarEvent.StartDate.ToString("MMM d, yyyy", CultureInfo.CurrentCulture)
                : $"{calendarEvent.StartDate:MMM d, yyyy} - {calendarEvent.EndDate:MMM d, yyyy}";
        }

        private static string? BuildEventTimeLabel(DailySummaryEvent calendarEvent)
        {
            if (!calendarEvent.StartTime.HasValue && !calendarEvent.EndTime.HasValue)
            {
                return null;
            }

            static string FormatTime(TimeSpan value) => DateTime.Today.Add(value).ToString("t", CultureInfo.CurrentCulture);

            if (calendarEvent.StartTime.HasValue && calendarEvent.EndTime.HasValue)
            {
                return $"{FormatTime(calendarEvent.StartTime.Value)} - {FormatTime(calendarEvent.EndTime.Value)}";
            }

            if (calendarEvent.StartTime.HasValue)
            {
                return $"Starts {FormatTime(calendarEvent.StartTime.Value)}";
            }

            return $"Ends {FormatTime(calendarEvent.EndTime!.Value)}";
        }

        private static DateTime AlignToWeekStart(DateTime date, DayOfWeek startDay)
        {
            while (date.DayOfWeek != startDay)
            {
                date = date.AddDays(-1);
            }

            return date.Date;
        }

        private static IReadOnlyList<PropertyRecap> BuildPropertyRecaps(
            IReadOnlyList<int> propertyIds,
            IReadOnlyDictionary<int, string> propertyNames,
            List<PassOnLog> logs,
            List<BulletinPost> posts,
            List<SalesLeadSubmission> salesLeads,
            IReadOnlyList<DailySummaryWorkOrder> workOrders,
            IReadOnlyList<DailySummaryPackageLog> packages,
            IReadOnlyList<DailySummaryLostFound> lostFoundEntries,
            IReadOnlyList<DailySummaryEvent> events,
            IReadOnlyList<DailySummaryAnnouncement> announcements,
            IReadOnlyList<DailySummarySchedule> schedules,
            IReadOnlyDictionary<int, int> pmFrequencies,
            IReadOnlyList<DailySummaryPmSession> completedPms,
            IReadOnlyList<DailySummaryPmDue> duePms,
            IReadOnlyList<DailySummaryPmDue> overduePms)
        {
            var contexts = new Dictionary<int, PropertyRecap>();
            var propertyIdSet = propertyIds != null ? new HashSet<int>(propertyIds) : new HashSet<int>();

            PropertyRecap GetContext(int propertyId, string? fallbackName = null)
            {
                if (!contexts.TryGetValue(propertyId, out var recap))
                {
                    var displayName = ResolvePropertyDisplayName(propertyId, fallbackName);
                    recap = new PropertyRecap(propertyId, displayName);
                    contexts[propertyId] = recap;
                }

                return recap;
            }

            string ResolvePropertyDisplayName(int propertyId, string? fallback)
            {
                if (propertyNames != null && propertyNames.TryGetValue(propertyId, out var known) && !string.IsNullOrWhiteSpace(known))
                {
                    return known;
                }

                if (!string.IsNullOrWhiteSpace(fallback))
                {
                    return fallback;
                }

                return propertyId > 0 ? $"Property #{propertyId}" : "Your Properties";
            }

            IEnumerable<int> ResolveTargets(IReadOnlyCollection<int>? candidates)
            {
                if (candidates != null && candidates.Count > 0)
                {
                    var filtered = propertyIdSet.Count == 0
                        ? candidates
                        : candidates.Where(propertyIdSet.Contains).ToList();

                    if (filtered.Any())
                    {
                        return filtered;
                    }
                }

                if (propertyIdSet.Count > 0)
                {
                        return propertyIdSet;
                }

                return new[] { 0 };
            }

            if (propertyIds != null)
            {
                foreach (var id in propertyIds)
                {
                    GetContext(id);
                }
            }

            if (announcements != null)
            {
                foreach (var announcement in announcements)
                {
                    var context = GetContext(announcement.PropertyId, announcement.PropertyName);
                    if (!context.Announcements.Any())
                    {
                        context.Announcements.Add(announcement);
                    }
                }
            }

            if (workOrders != null)
            {
                foreach (var order in workOrders)
                {
                    var targetIds = ResolveTargets(order.PropertyIds).ToList();
                    var fallbackName = order.PropertyNames.FirstOrDefault();
                    if (!targetIds.Any())
                    {
                        targetIds.Add(0);
                    }

                    foreach (var propertyId in targetIds)
                    {
                        var context = GetContext(propertyId, fallbackName);
                        if (!context.WorkOrders.Any(existing => existing.Id == order.Id))
                        {
                            context.WorkOrders.Add(order);
                        }
                    }
                }
            }

            if (logs != null)
            {
                foreach (var log in logs)
                {
                    var ids = log.Properties?
                        .Select(p => p.PropertyId)
                        .Where(id => propertyIdSet.Count == 0 || propertyIdSet.Contains(id))
                        .Distinct()
                        .ToList() ?? new List<int>();

                    if (!ids.Any())
                    {
                        ids = propertyIdSet.Count > 0 ? propertyIdSet.ToList() : new List<int> { 0 };
                    }

                    foreach (var propertyId in ids)
                    {
                        var context = GetContext(propertyId);
                        if (!context.PassOnLogs.Any(existing => existing.Id == log.Id))
                        {
                            context.PassOnLogs.Add(log);
                        }
                    }
                }
            }

            if (posts != null)
            {
                foreach (var post in posts)
                {
                    var context = GetContext(post.PropertyId, post.Property?.Name);
                    if (!context.Bulletins.Any(existing => existing.Id == post.Id))
                    {
                        context.Bulletins.Add(post);
                    }
                }
            }

            if (packages != null)
            {
                foreach (var package in packages)
                {
                    var context = GetContext(package.PropertyId, package.PropertyName);
                    if (!context.Packages.Any(existing => existing.Id == package.Id))
                    {
                        context.Packages.Add(package);
                    }
                }
            }

            if (lostFoundEntries != null)
            {
                foreach (var entry in lostFoundEntries)
                {
                    var context = GetContext(entry.PropertyId, entry.PropertyName);
                    if (!context.LostFoundEntries.Any(existing => existing.Id == entry.Id))
                    {
                        context.LostFoundEntries.Add(entry);
                    }
                }
            }

            if (events != null)
            {
                foreach (var calendarEvent in events)
                {
                    var targetIds = ResolveTargets(calendarEvent.PropertyIds).ToList();
                    if (!targetIds.Any())
                    {
                        targetIds.Add(0);
                    }

                    foreach (var propertyId in targetIds)
                    {
                        var context = GetContext(propertyId, calendarEvent.PropertyNames.FirstOrDefault());
                        if (!context.Events.Any(existing => ReferenceEquals(existing, calendarEvent)))
                        {
                            context.Events.Add(calendarEvent);
                        }
                    }
                }
            }

            if (schedules != null)
            {
                foreach (var schedule in schedules)
                {
                    var context = GetContext(schedule.PropertyId, schedule.PropertyName);
                    var target = schedule.IsUpcoming ? context.UpcomingSchedules : context.CurrentSchedules;
                    if (!target.Any(existing => existing.DetailPath == schedule.DetailPath))
                    {
                        target.Add(schedule);
                    }
                }
            }

            if (salesLeads != null)
            {
                foreach (var lead in salesLeads)
                {
                    var context = GetContext(lead.PropertyId, lead.Property?.Name);
                    context.SalesLeads.Add(lead);
                }
            }

            if (completedPms != null)
            {
                foreach (var pm in completedPms)
                {
                    var context = GetContext(pm.PropertyId);
                    context.CompletedPms.Add(pm);
                }
            }

            if (duePms != null)
            {
                foreach (var due in duePms)
                {
                    var context = GetContext(due.PropertyId);
                    context.DuePms.Add(due);
                }
            }

            if (overduePms != null)
            {
                foreach (var entry in overduePms)
                {
                    var context = GetContext(entry.PropertyId);
                    context.OverduePms.Add(entry);
                }
            }

            if (pmFrequencies != null && pmFrequencies.Count > 0)
            {
                foreach (var context in contexts.Values)
                {
                    if (pmFrequencies.TryGetValue(context.PropertyId, out var frequency))
                    {
                        context.FrequencyPerYear = frequency;
                    }
                }
            }

            if (contexts.Count == 0)
            {
                return Array.Empty<PropertyRecap>();
            }

            return contexts.Values
                .OrderBy(c => c.PropertyName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private sealed class PropertyRecap
        {
            public PropertyRecap(int propertyId, string propertyName)
            {
                PropertyId = propertyId;
                PropertyName = propertyName;
            }

            public int PropertyId { get; }
            public string PropertyName { get; }
            public List<DailySummaryWorkOrder> WorkOrders { get; } = new();
            public List<PassOnLog> PassOnLogs { get; } = new();
            public List<BulletinPost> Bulletins { get; } = new();
            public List<DailySummaryPackageLog> Packages { get; } = new();
            public List<DailySummaryLostFound> LostFoundEntries { get; } = new();
            public List<DailySummaryAnnouncement> Announcements { get; } = new();
            public List<DailySummaryEvent> Events { get; } = new();
            public List<DailySummarySchedule> CurrentSchedules { get; } = new();
            public List<DailySummarySchedule> UpcomingSchedules { get; } = new();
            public List<SalesLeadSubmission> SalesLeads { get; } = new();
            public int FrequencyPerYear { get; set; }
            public List<DailySummaryPmSession> CompletedPms { get; } = new();
            public List<DailySummaryPmDue> DuePms { get; } = new();
            public List<DailySummaryPmDue> OverduePms { get; } = new();
        }

        private sealed class EmailPalette
        {
            public string Background { get; init; } = "#020714";
            public string Panel { get; init; } = "#0b1930";
            public string HeroBackground { get; init; } = "#102642";
            public string Border { get; init; } = "#133152";
            public string Text { get; init; } = "#f2f6ff";
            public string Muted { get; init; } = "#90a4c2";
            public string Accent { get; init; } = "#1cb5e0";
            public string AccentAlt { get; init; } = "#66d8ff";
            public string BadgeBackground { get; init; } = "#122947";
            public string ListItemBackground { get; init; } = "#0d1c33";

            public static EmailPalette Default { get; } = new EmailPalette();
        }

        private sealed class DailySummaryWorkOrder
        {
            public int Id { get; set; }
            public string Issue { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public string? Location { get; set; }
            public string? DepartmentName { get; set; }
            public List<string> PropertyNames { get; set; } = new();
            public List<int> PropertyIds { get; set; } = new();
            public DateTime CreatedAt { get; set; }
            public DateTime DueDate { get; set; }
            public string DetailPath { get; set; } = string.Empty;
        }

        private sealed class DailySummaryAnnouncement
        {
            public int PropertyId { get; set; }
            public string PropertyName { get; set; } = string.Empty;
            public string Content { get; set; } = string.Empty;
            public DateTime? UpdatedAt { get; set; }
            public string? UpdatedByName { get; set; }
            public List<DailySummaryAttachment> Attachments { get; set; } = new();
        }

        private sealed class DailySummaryAttachment
        {
            public string FileName { get; set; } = string.Empty;
            public string DownloadPath { get; set; } = string.Empty;
        }

        private sealed class DailySummaryEvent
        {
            public string? Title { get; set; }
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
            public TimeSpan? StartTime { get; set; }
            public TimeSpan? EndTime { get; set; }
            public string CategoryName { get; set; } = string.Empty;
            public List<string> PropertyNames { get; set; } = new();
            public List<int> PropertyIds { get; set; } = new();
            public string DetailPath { get; set; } = string.Empty;
        }

        private sealed class DailySummaryPackageLog
        {
            public int Id { get; set; }
            public int PropertyId { get; set; }
            public string PropertyName { get; set; } = string.Empty;
            public string? RecipientName { get; set; }
            public string? RoomNumber { get; set; }
            public string? Carrier { get; set; }
            public string? TrackingNumber { get; set; }
            public string? StorageLocation { get; set; }
            public DateTime? PackageReceivedDate { get; set; }
            public DateTime LoggedAt { get; set; }
            public string DetailPath { get; set; } = string.Empty;
        }

        private sealed class DailySummaryLostFound
        {
            public int Id { get; set; }
            public int PropertyId { get; set; }
            public string Title { get; set; } = string.Empty;
            public string PropertyName { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public string? Type { get; set; }
            public DateTime CreatedAt { get; set; }
            public string DetailPath { get; set; } = string.Empty;
        }

        private sealed class DailySummarySchedule
        {
            public int PropertyId { get; set; }
            public string PropertyName { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public DateTime WeekStart { get; set; }
            public DateTime WeekEnd { get; set; }
            public string DetailPath { get; set; } = string.Empty;
            public bool IsUpcoming { get; set; }
        }

        private sealed class DailySummaryPmSession
        {
            public int PropertyId { get; set; }
            public string RoomNumber { get; set; } = string.Empty;
            public DateTime CompletedAtUtc { get; set; }
            public double DurationSeconds { get; set; }
        }

        private sealed class DailySummaryPmDue
        {
            public int PropertyId { get; set; }
            public string RoomNumber { get; set; } = string.Empty;
            public DateTime DueAtUtc { get; set; }
        }

        private sealed class PreventiveMaintenanceSummaryData
        {
            public IReadOnlyDictionary<int, int> Frequencies { get; init; } = new Dictionary<int, int>();
            public IReadOnlyList<DailySummaryPmSession> Completed { get; init; } = Array.Empty<DailySummaryPmSession>();
            public IReadOnlyList<DailySummaryPmDue> DueSoon { get; init; } = Array.Empty<DailySummaryPmDue>();
            public IReadOnlyList<DailySummaryPmDue> Overdue { get; init; } = Array.Empty<DailySummaryPmDue>();
        }

        private static string BuildUserDisplayName(ApplicationUser? user)
        {
            if (user == null)
            {
                return "there";
            }

            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(user.FirstName))
            {
                parts.Add(user.FirstName);
            }
            if (!string.IsNullOrWhiteSpace(user.LastName))
            {
                parts.Add(user.LastName);
            }

            if (parts.Count > 0)
            {
                return string.Join(" ", parts);
            }

            return string.IsNullOrWhiteSpace(user.Email) ? "there" : user.Email!;
        }

        private static string BuildInquiryListHtml(SalesLeadSubmission lead)
        {
            var labels = (lead.InquiryTypes ?? string.Empty)
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(key => key.Trim())
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Select(GetInquiryLabel)
                .Select(WebUtility.HtmlEncode)
                .ToList();

            if (labels.Count == 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            builder.Append(string.Join("<br/>", labels));

            if (!string.IsNullOrWhiteSpace(lead.InquiryOtherDetails))
            {
                var encoded = WebUtility.HtmlEncode(lead.InquiryOtherDetails);
                builder.Append("<br/><em>Other:</em> ").Append(encoded);
            }

            return builder.ToString();
        }

        private static string GetInquiryLabel(string key)
        {
            if (SalesInquiryLabels.TryGetValue(key, out var label))
            {
                return label;
            }

            return key;
        }

        private static string BuildDateRangeDescription(DateTime? start, DateTime? end, TimeZoneInfo timeZone)
        {
            if (!start.HasValue && !end.HasValue)
            {
                return "Not provided";
            }

            if (start.HasValue && end.HasValue)
            {
                var startText = FormatUserLocal(start.Value, timeZone, "MMM d");
                var endText = FormatUserLocal(end.Value, timeZone, "MMM d");
                return $"{startText} - {endText}";
            }

            if (start.HasValue)
            {
                var startText = FormatUserLocal(start.Value, timeZone, "MMM d");
                return $"Starting {startText}";
            }

            var endTextOnly = FormatUserLocal(end!.Value, timeZone, "MMM d");
            return $"Ending {endTextOnly}";
        }

        private static string BuildBudgetDescription(decimal? min, decimal? max)
        {
            if (!min.HasValue && !max.HasValue)
            {
                return "Not provided";
            }

            var culture = CultureInfo.CurrentCulture;
            if (min.HasValue && max.HasValue)
            {
                var minText = min.Value.ToString("C0", culture);
                var maxText = max.Value.ToString("C0", culture);
                return $"{minText} - {maxText}";
            }

            if (min.HasValue)
            {
                var minText = min.Value.ToString("C0", culture);
                return $"{minText}+";
            }

            var maxOnly = max!.Value.ToString("C0", culture);
            return $"Up to {maxOnly}";
        }

        private static string BuildPlainTextHtml(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var encoded = WebUtility.HtmlEncode(value);
            return encoded
                .Replace("\r\n", "<br />", StringComparison.Ordinal)
                .Replace("\n", "<br />", StringComparison.Ordinal)
                .Replace("\r", "<br />", StringComparison.Ordinal);
        }

        private static DateTimeOffset GetNextRun(DateTimeOffset from)
        {
            var localNow = from.ToLocalTime();
            var target = new DateTimeOffset(localNow.Year, localNow.Month, localNow.Day, 6, 0, 0, localNow.Offset);
            if (localNow >= target)
            {
                target = target.AddDays(1);
            }

            return target.ToUniversalTime();
        }

        private static string BuildRichTextPreview(string? content, int maxCharacters)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return string.Empty;
            }

            var plainText = RichTextRenderer.ToPlainText(content);
            if (string.IsNullOrWhiteSpace(plainText))
            {
                return string.Empty;
            }

            if (maxCharacters <= 0 || plainText.Length <= maxCharacters)
            {
                return RichTextRenderer.ToHtml(content);
            }

            var displayWithBreaks = RichTextRenderer.ToPlainTextWithLineBreaks(content);
            if (string.IsNullOrWhiteSpace(displayWithBreaks))
            {
                displayWithBreaks = plainText;
            }

            var truncated = displayWithBreaks[..Math.Min(maxCharacters, displayWithBreaks.Length)].TrimEnd();
            if (truncated.Length < displayWithBreaks.Length)
            {
                truncated = $"{truncated}...";
            }

            return BuildPlainTextHtml(truncated);
        }

        private static TimeZoneInfo ResolveUserTimeZone(ApplicationUser user)
        {
            var normalized = DefaultTimeZoneProvider.NormalizeForStorage(user.TimeZoneId);
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(normalized);
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.Utc;
            }
            catch (InvalidTimeZoneException)
            {
                return TimeZoneInfo.Utc;
            }
        }

        private static string FormatUserLocal(DateTime utcDateTime, TimeZoneInfo timeZone, string format)
        {
            var utc = utcDateTime.Kind switch
            {
                DateTimeKind.Utc => utcDateTime,
                DateTimeKind.Unspecified => DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc),
                DateTimeKind.Local => utcDateTime.ToUniversalTime(),
                _ => utcDateTime
            };

            var localized = TimeZoneInfo.ConvertTimeFromUtc(utc, timeZone);
            return localized.ToString(format, CultureInfo.CurrentCulture);
        }

        private static string BuildAbsoluteUrl(string relativeUrl, string? baseUrl)
        {
            if (string.IsNullOrWhiteSpace(relativeUrl))
            {
                return string.IsNullOrWhiteSpace(baseUrl) ? "/" : baseUrl!;
            }

            if (Uri.TryCreate(relativeUrl, UriKind.Absolute, out var absolute))
            {
                return absolute.ToString();
            }

            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                return relativeUrl;
            }

            return $"{baseUrl!.TrimEnd('/')}/{relativeUrl.TrimStart('/')}";
        }
    }
}
