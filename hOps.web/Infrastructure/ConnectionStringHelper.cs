using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace hOps.web.Infrastructure;

internal static class ConnectionStringHelper
{
    private static readonly string[] ConnectionStringFallbackKeys =
    [
        "ConnectionStrings:DefaultConnection",
        "ConnectionStrings__DefaultConnection",
        "DATABASE_URL",
        "DATABASE_URL_FULL",
        "POSTGRES_URL",
        "POSTGRESQL_URL"
    ];

    internal static string GetDefaultConnectionString(IConfiguration configuration)
    {
        var configured = configuration.GetConnectionString("DefaultConnection");
        if (IsPlaceholder(configured))
        {
            configured = null;
        }

        if (string.IsNullOrWhiteSpace(configured))
        {
            foreach (var key in ConnectionStringFallbackKeys)
            {
                var candidate = configuration[key];
                if (string.IsNullOrWhiteSpace(candidate) || IsPlaceholder(candidate))
                {
                    continue;
                }

                configured = candidate;
                break;
            }
        }

        return NormalizePostgresConnectionString(configured);
    }

    internal static string NormalizePostgresConnectionString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = TrimWrappingCharacters(value);
        var schemeIndex = FindPostgresSchemeIndex(trimmed);

        if (schemeIndex > 0)
        {
            trimmed = trimmed[schemeIndex..];
        }

        if (!LooksLikePostgresUri(trimmed))
        {
            return trimmed;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return trimmed;
        }

        var builder = new StringBuilder();
        builder.Append("Host=").Append(uri.Host).Append(';');

        var port = uri.Port > 0 ? uri.Port : 5432;
        builder.Append("Port=").Append(port).Append(';');

        var database = uri.AbsolutePath.Trim('/');
        if (!string.IsNullOrEmpty(database))
        {
            builder.Append("Database=").Append(Uri.UnescapeDataString(database)).Append(';');
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            var userInfoParts = uri.UserInfo.Split(':', 2, StringSplitOptions.TrimEntries);
            if (userInfoParts.Length > 0 && !string.IsNullOrEmpty(userInfoParts[0]))
            {
                builder.Append("Username=").Append(Uri.UnescapeDataString(userInfoParts[0])).Append(';');
            }

            if (userInfoParts.Length == 2 && !string.IsNullOrEmpty(userInfoParts[1]))
            {
                builder.Append("Password=").Append(Uri.UnescapeDataString(userInfoParts[1])).Append(';');
            }
        }

        var hasSslMode = false;
        var hasTrustServerCertificate = false;

        foreach (var (key, normalizedValue) in ParseSupportedQueryParameters(uri.Query))
        {
            builder.Append(key).Append('=').Append(normalizedValue).Append(';');

            if (key.Equals("SSL Mode", StringComparison.OrdinalIgnoreCase))
            {
                hasSslMode = true;
            }

            if (key.Equals("Trust Server Certificate", StringComparison.OrdinalIgnoreCase))
            {
                hasTrustServerCertificate = true;
            }
        }

        if (!hasSslMode)
        {
            builder.Append("SSL Mode=Require;");
        }

        if (!hasTrustServerCertificate)
        {
            builder.Append("Trust Server Certificate=true;");
        }

        return builder.ToString().TrimEnd(';');
    }

    private static string TrimWrappingCharacters(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        ReadOnlySpan<char> span = value.AsSpan().Trim();

        int start = 0;
        int end = span.Length - 1;

        while (start <= end && IsWrapper(span[start]))
        {
            start++;
        }

        while (end >= start && IsWrapper(span[end]))
        {
            end--;
        }

        return start > 0 || end < span.Length - 1
            ? span[start..(end + 1)].ToString()
            : span.ToString();
    }

    private static bool IsWrapper(char c) => c is '"' or '\'' or '`';

    private static bool LooksLikePostgresUri(string value) =>
        value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase);

    private static int FindPostgresSchemeIndex(string value)
    {
        var index = value.IndexOf("postgres://", StringComparison.OrdinalIgnoreCase);
        if (index >= 0)
        {
            return index;
        }

        return value.IndexOf("postgresql://", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPlaceholder(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var trimmed = value.Trim();
        var hasLt = trimmed.IndexOf('<') >= 0;
        var hasGt = trimmed.IndexOf('>') >= 0;
        if (!hasLt && !hasGt)
        {
            return false;
        }

        // Treat strings that include template markers like <POSTGRES_HOST> as unset so env vars can override.
        var tokenStart = trimmed.IndexOf('<');
        var tokenEnd = trimmed.IndexOf('>', tokenStart + 1);
        return tokenStart >= 0 && tokenEnd > tokenStart;
    }

    private static IEnumerable<(string Key, string Value)> ParseSupportedQueryParameters(string rawQuery)
    {
        if (string.IsNullOrWhiteSpace(rawQuery) || rawQuery.Length <= 1)
        {
            yield break;
        }

        var trimmed = rawQuery.TrimStart('?');
        var pairs = trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var pair in pairs)
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 0)
            {
                continue;
            }

            var key = Uri.UnescapeDataString(parts[0]);
            var value = parts.Length == 2
                ? Uri.UnescapeDataString(parts[1])
                : "true";

            if (key.Equals("sslmode", StringComparison.OrdinalIgnoreCase))
            {
                yield return ("SSL Mode", value);
                continue;
            }

            if (key.Equals("ssl_mode", StringComparison.OrdinalIgnoreCase))
            {
                yield return ("SSL Mode", value);
                continue;
            }

            if (key.Equals("trust_server_certificate", StringComparison.OrdinalIgnoreCase))
            {
                yield return ("Trust Server Certificate", value);
            }
        }
    }
}
