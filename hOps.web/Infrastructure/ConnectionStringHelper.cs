using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace hOps.web.Infrastructure;

internal static class ConnectionStringHelper
{
    private static bool? _diagnosticsEnabled;

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
        var configured = NormalizeRawConnectionString(configuration.GetConnectionString("DefaultConnection"));
        LogConnectionStringDiagnostics(configuration, "ConnectionStrings:DefaultConnection", configured, "configured");

        if (IsPlaceholder(configured))
        {
            LogConnectionStringDiagnostics(configuration, "ConnectionStrings:DefaultConnection", configured, "placeholder");
            configured = null;
        }

        if (string.IsNullOrWhiteSpace(configured))
        {
            foreach (var key in ConnectionStringFallbackKeys)
            {
                var candidate = NormalizeRawConnectionString(configuration[key]);
                LogConnectionStringDiagnostics(configuration, key, candidate, "candidate");

                if (string.IsNullOrWhiteSpace(candidate) || IsPlaceholder(candidate))
                {
                    if (IsPlaceholder(candidate))
                    {
                        LogConnectionStringDiagnostics(configuration, key, candidate, "placeholder");
                    }

                    continue;
                }

                configured = candidate;
                break;
            }
        }

        LogConnectionStringDiagnostics(configuration, "effective", configured, "selected");
        return NormalizePostgresConnectionString(configured);
    }

    private static string? NormalizeRawConnectionString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var trimmed = TrimWrappingCharacters(value);

        if (TryExtractConnectionStringFromJson(trimmed, out var extracted))
        {
            return extracted;
        }

        return trimmed;
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

    private static bool TryExtractConnectionStringFromJson(string value, out string connectionString)
    {
        connectionString = string.Empty;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();

        if (TryParseJsonPayload(trimmed, out connectionString))
        {
            return true;
        }

        var braceSlice = SliceBetween(trimmed, '{', '}');
        if (braceSlice is { Length: > 0 } && TryParseJsonPayload(braceSlice, out connectionString))
        {
            return true;
        }

        var bracketSlice = SliceBetween(trimmed, '[', ']');
        if (bracketSlice is { Length: > 0 } && TryParseJsonPayload(bracketSlice, out connectionString))
        {
            return true;
        }

        return false;
    }

    private static bool IsDiagnosticsEnabled(IConfiguration configuration)
    {
        if (_diagnosticsEnabled.HasValue)
        {
            return _diagnosticsEnabled.Value;
        }

        var raw =
            configuration["ConnectionStrings:LogDiagnostics"] ??
            configuration["ConnectionStrings__LogDiagnostics"] ??
            configuration["LOG_CONNECTION_DIAGNOSTICS"] ??
            Environment.GetEnvironmentVariable("LOG_CONNECTION_DIAGNOSTICS");

        _diagnosticsEnabled = TryParseBooleanFlag(raw);
        return _diagnosticsEnabled.Value;
    }

    private static void LogConnectionStringDiagnostics(IConfiguration configuration, string sourceKey, string? value, string stage)
    {
        if (!IsDiagnosticsEnabled(configuration))
        {
            return;
        }

        Console.WriteLine($"[ConnectionStringDiagnostics] stage={stage} source={sourceKey} info={DescribeValue(value)}");
    }

    private static string DescribeValue(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "empty";
        }

        var trimmed = value.Trim();
        var firstCharCode = trimmed.Length > 0 ? ((int)trimmed[0]).ToString() : "none";
        var hasBraces = trimmed.Contains('{');
        var hasEquals = trimmed.Contains('=');
        var looksUri = LooksLikePostgresUri(trimmed);
        var whitespacePrefix = value.Length - value.TrimStart().Length;
        var whitespaceSuffix = value.Length - value.TrimEnd().Length;
        var asciiPreview = string.Join(",", trimmed.Take(Math.Min(5, trimmed.Length)).Select(c => ((int)c).ToString()));

        return $"len={trimmed.Length}, firstCharCode={firstCharCode}, hasBrace={hasBraces}, hasEquals={hasEquals}, looksUri={looksUri}, leadingWhitespace={whitespacePrefix}, trailingWhitespace={whitespaceSuffix}, asciiPrefix=[{asciiPreview}]";
    }

    private static bool TryParseBooleanFlag(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        raw = raw.Trim();

        if (bool.TryParse(raw, out var parsed))
        {
            return parsed;
        }

        if (string.Equals(raw, "1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(raw, "yes", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(raw, "0", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(raw, "no", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(raw, "false", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return false;
    }

    private static string? SliceBetween(string value, char startChar, char endChar)
    {
        var start = value.IndexOf(startChar);
        if (start < 0)
        {
            return null;
        }

        var end = value.LastIndexOf(endChar);
        if (end <= start)
        {
            return null;
        }

        return value[start..(end + 1)].Trim();
    }

    private static bool TryParseJsonPayload(string payload, out string connectionString)
    {
        connectionString = string.Empty;

        if (string.IsNullOrWhiteSpace(payload) || (payload[0] != '{' && payload[0] != '['))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            var candidate = ExtractConnectionStringFromElement(document.RootElement);
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                connectionString = candidate.Trim();
                return true;
            }
        }
        catch (JsonException)
        {
            // Text wasn't valid JSON – ignore.
        }

        return false;
    }

    private static string? ExtractConnectionStringFromElement(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                return element.GetString();

            case JsonValueKind.Object:
                if (TryGetPropertyCaseInsensitive(element, "ConnectionStrings", out var connectionStringsElement))
                {
                    var nested = ExtractConnectionStringFromElement(connectionStringsElement);
                    if (!string.IsNullOrWhiteSpace(nested))
                    {
                        return nested;
                    }
                }

                if (TryGetPropertyCaseInsensitive(element, "DefaultConnection", out var defaultElement))
                {
                    var nested = ExtractConnectionStringFromElement(defaultElement);
                    if (!string.IsNullOrWhiteSpace(nested))
                    {
                        return nested;
                    }
                }

                foreach (var property in element.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.String)
                    {
                        var candidate = property.Value.GetString();
                        if (!string.IsNullOrWhiteSpace(candidate))
                        {
                            return candidate;
                        }
                    }
                }

                break;

            default:
                if (element.ValueKind == JsonValueKind.Array)
                {
                    foreach (var arrayElement in element.EnumerateArray())
                    {
                        var candidate = ExtractConnectionStringFromElement(arrayElement);
                        if (!string.IsNullOrWhiteSpace(candidate))
                        {
                            return candidate;
                        }
                    }
                }

                break;
        }

        return null;
    }

    private static bool TryGetPropertyCaseInsensitive(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.NameEquals(propertyName) ||
                    property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
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
