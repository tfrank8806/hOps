using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using hOps.web.Data;
using hOps.web.Models;
using hOps.web.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace hOps.web.Services
{
    public class CalendarReminderService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<CalendarReminderService> _logger;
        private readonly IConfiguration _configuration;

        private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(5);

        public CalendarReminderService(
            IServiceProvider serviceProvider,
            ILogger<CalendarReminderService> logger,
            IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessDueRemindersAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process calendar reminders.");
                }

                try
                {
                    await Task.Delay(PollInterval, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        private async Task ProcessDueRemindersAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var now = DateTime.UtcNow;
            var baseUrl = (_configuration["App:BaseUrl"] ?? _configuration["AppBaseUrl"])?.TrimEnd('/');

            var reminders = await dbContext.CalendarEventReminders
                .Include(r => r.CalendarEvent).ThenInclude(e => e.EventProperties).ThenInclude(ep => ep.Property)
                .Include(r => r.CalendarEvent).ThenInclude(e => e.TargetDepartment)
                .Where(r => !r.IsSent && r.ScheduledSendUtc <= now)
                .OrderBy(r => r.ScheduledSendUtc)
                .Take(50)
                .ToListAsync(cancellationToken);

            if (reminders.Count == 0)
            {
                return;
            }

            foreach (var reminder in reminders)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    var recipients = await ResolveRecipientsAsync(dbContext, reminder.CalendarEvent, cancellationToken);
                    if (recipients.Count == 0)
                    {
                        reminder.IsSent = true;
                        reminder.SentAtUtc = now;
                        continue;
                    }

                    var descriptor = DescribeReminder(reminder.ReminderType);
                    var dateLabel = reminder.OccurrenceStartUtc.ToString("MMM d, yyyy");
                    string? timeLabel = null;
                    if (reminder.CalendarEvent.StartTime.HasValue)
                    {
                        timeLabel = DateTime.Today.Add(reminder.CalendarEvent.StartTime.Value).ToString("t");
                    }
                    var dateTimeLabel = timeLabel == null ? dateLabel : $"{dateLabel} at {timeLabel}";
                    var propertyNames = reminder.CalendarEvent.EventProperties?
                        .Select(ep => ep.Property?.Name)
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .Select(name => name!)
                        .Distinct()
                        .ToList() ?? new List<string>();
                    var locationText = propertyNames.Count > 0 ? string.Join(", ", propertyNames) : null;
                    var message = BuildReminderMessage(reminder.CalendarEvent.Title, descriptor, dateTimeLabel, locationText);
                    var linkUrl = BuildCalendarLink(reminder, baseUrl);

                    foreach (var userId in recipients)
                    {
                        dbContext.UserNotifications.Add(new UserNotification
                        {
                            UserId = userId,
                            Type = "calendar",
                            Title = $"Reminder: {reminder.CalendarEvent.Title}",
                            Content = message,
                            LinkUrl = linkUrl,
                            CreatedAt = now,
                            IsRead = false
                        });
                    }

                    reminder.IsSent = true;
                    reminder.SentAtUtc = now;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to dispatch reminder for event {EventId}", reminder.CalendarEventId);
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        private static string BuildCalendarLink(CalendarEventReminder reminder, string? baseUrl)
        {
            var occurrence = reminder.OccurrenceStartUtc;
            var relative = $"/Calendar?month={occurrence.Month}&year={occurrence.Year}";
            return string.IsNullOrWhiteSpace(baseUrl) ? relative : $"{baseUrl}{relative}";
        }

        private static string BuildReminderMessage(string title, string descriptor, string dateLabel, string? location)
        {
            var message = $"{title} is {descriptor} ({dateLabel}).";
            if (!string.IsNullOrWhiteSpace(location))
            {
                message += $" Location: {location}.";
            }

            return message;
        }

        private static string DescribeReminder(CalendarEventReminderOffset offset)
        {
            return offset switch
            {
                CalendarEventReminderOffset.DayOfEvent => "today",
                CalendarEventReminderOffset.OneDayBefore => "tomorrow",
                CalendarEventReminderOffset.TwoDaysBefore => "in 2 days",
                CalendarEventReminderOffset.OneWeekBefore => "next week",
                _ => "soon"
            };
        }

        private static async Task<List<string>> ResolveRecipientsAsync(
            ApplicationDbContext dbContext,
            CalendarEvent calendarEvent,
            CancellationToken cancellationToken)
        {
            var propertyIds = calendarEvent.EventProperties?
                .Select(ep => ep.PropertyId)
                .Distinct()
                .ToList() ?? new List<int>();

            if (propertyIds.Count == 0)
            {
                return new List<string>();
            }

            List<int> targetDepartmentIds;
            if (calendarEvent.NotifyAllDepartments)
            {
                targetDepartmentIds = await dbContext.Departments
                    .Where(d => d.PropertyId.HasValue && propertyIds.Contains(d.PropertyId.Value))
                    .Select(d => d.Id)
                    .ToListAsync(cancellationToken);
            }
            else if (calendarEvent.TargetDepartmentId.HasValue)
            {
                var valid = await dbContext.Departments
                    .Where(d => d.Id == calendarEvent.TargetDepartmentId.Value &&
                                (!d.PropertyId.HasValue || propertyIds.Contains(d.PropertyId.Value)))
                    .Select(d => d.Id)
                    .FirstOrDefaultAsync(cancellationToken);

                targetDepartmentIds = valid == 0 ? new List<int>() : new List<int> { valid };
            }
            else
            {
                targetDepartmentIds = new List<int>();
            }

            if (targetDepartmentIds.Count == 0)
            {
                return new List<string>();
            }

            return await dbContext.UserDepartmentSubscriptions
                .Where(sub => targetDepartmentIds.Contains(sub.DepartmentId))
                .Select(sub => sub.UserId)
                .Distinct()
                .ToListAsync(cancellationToken);
        }
    }
}
