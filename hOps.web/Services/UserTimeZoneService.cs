using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using hOps.web.Utilities;
using Microsoft.AspNetCore.Http;

namespace hOps.web.Services
{
    public interface IUserTimeZoneService
    {
        TimeZoneInfo GetTimeZone();
        DateTime ConvertToUserTime(DateTime utcDateTime);
        string FormatLocal(DateTime utcDateTime, string format);
    }

    public class UserTimeZoneService : IUserTimeZoneService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private static readonly ConcurrentDictionary<string, TimeZoneInfo> Cache = new();

        public UserTimeZoneService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public TimeZoneInfo GetTimeZone()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                return TimeZoneInfo.Utc;
            }

            if (httpContext.Items.TryGetValue("UserTimeZoneId", out var itemValue) &&
                itemValue is string itemString &&
                !string.IsNullOrWhiteSpace(itemString))
            {
                var normalized = DefaultTimeZoneProvider.NormalizeForStorage(itemString);
                return GetTimeZoneInfo(normalized);
            }

            if (httpContext.Session != null)
            {
                var sessionValue = httpContext.Session.GetString("UserTimeZoneId");
                if (!string.IsNullOrWhiteSpace(sessionValue))
                {
                    var normalized = DefaultTimeZoneProvider.NormalizeForStorage(sessionValue);
                    return GetTimeZoneInfo(normalized);
                }
            }

            return GetTimeZoneInfo(DefaultTimeZoneProvider.DefaultTimeZoneId);
        }

        public DateTime ConvertToUserTime(DateTime utcDateTime)
        {
            if (utcDateTime.Kind == DateTimeKind.Local)
            {
                utcDateTime = utcDateTime.ToUniversalTime();
            }
            else if (utcDateTime.Kind == DateTimeKind.Unspecified)
            {
                utcDateTime = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
            }

            var tz = GetTimeZone();
            return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, tz);
        }

        public string FormatLocal(DateTime utcDateTime, string format)
        {
            return ConvertToUserTime(utcDateTime).ToString(format);
        }

        private static TimeZoneInfo GetTimeZoneInfo(string id)
        {
            return Cache.GetOrAdd(id, key =>
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(key);
                }
                catch (TimeZoneNotFoundException)
                {
                    return TimeZoneInfo.Utc;
                }
                catch (InvalidTimeZoneException)
                {
                    return TimeZoneInfo.Utc;
                }
            });
        }
    }
}
