using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace hOps.web.Services
{
    /// <summary>
    /// Ensures Azure SQL connections have an access token when the connection string
    /// relies on Azure AD authentication instead of SQL logins.
    /// </summary>
    public sealed class AzureSqlAccessTokenInterceptor : DbConnectionInterceptor
    {
        private static readonly string[] SqlScopes = ["https://database.windows.net/.default"];
        private readonly TokenCredential _credential;

        public AzureSqlAccessTokenInterceptor(TokenCredential credential)
        {
            _credential = credential ?? throw new ArgumentNullException(nameof(credential));
        }

        public override async ValueTask<InterceptionResult> ConnectionOpeningAsync(
            DbConnection connection,
            ConnectionEventData eventData,
            InterceptionResult result,
            CancellationToken cancellationToken = default)
        {
            if (connection is SqlConnection sqlConnection &&
                ShouldAssignAccessToken(sqlConnection.ConnectionString))
            {
                await EnsureAccessTokenAsync(sqlConnection, cancellationToken);
            }

            return await base.ConnectionOpeningAsync(connection, eventData, result, cancellationToken);
        }

        public override InterceptionResult ConnectionOpening(
            DbConnection connection,
            ConnectionEventData eventData,
            InterceptionResult result)
        {
            if (connection is SqlConnection sqlConnection &&
                ShouldAssignAccessToken(sqlConnection.ConnectionString))
            {
                EnsureAccessTokenAsync(sqlConnection, CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
            }

            return base.ConnectionOpening(connection, eventData, result);
        }

        private async Task EnsureAccessTokenAsync(SqlConnection sqlConnection, CancellationToken cancellationToken)
        {
            var token = await _credential.GetTokenAsync(
                new TokenRequestContext(SqlScopes),
                cancellationToken);

            try
            {
                sqlConnection.AccessToken = token.Token;
            }
            catch (InvalidOperationException ex) when (IsAuthenticationConflict(ex))
            {
                // If the connection string already configures Authentication, SqlClient owns token acquisition.
            }
        }

        private static bool ShouldAssignAccessToken(string? connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return false;
            }

            if (!IsAzureSqlConnectionString(connectionString))
            {
                return false;
            }

            if (HasExplicitAuthentication(connectionString))
            {
                return false;
            }

            return true;
        }

        private static bool IsAzureSqlConnectionString(string connectionString)
        {
            try
            {
                var builder = new SqlConnectionStringBuilder(connectionString);

                if (!string.IsNullOrEmpty(builder.Password))
                {
                    return false;
                }

                if (!string.IsNullOrEmpty(builder.UserID))
                {
                    return true;
                }

                return builder.DataSource?.Contains("database.windows.net", StringComparison.OrdinalIgnoreCase) == true;
            }
            catch
            {
                return connectionString.Contains("database.windows.net", StringComparison.OrdinalIgnoreCase);
            }
        }

        private static bool HasExplicitAuthentication(string connectionString)
        {
            try
            {
                var builder = new SqlConnectionStringBuilder(connectionString);
                if (builder.Authentication is not SqlAuthenticationMethod.NotSpecified)
                {
                    return true;
                }
            }
            catch
            {
            }

            return ContainsAuthenticationKeyword(connectionString);
        }

        private static bool ContainsAuthenticationKeyword(string connectionString)
        {
            var segments = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);

            foreach (var segment in segments)
            {
                var trimmed = segment.Trim();
                if (trimmed.Length == 0)
                {
                    continue;
                }

                var equalsIndex = trimmed.IndexOf('=');
                if (equalsIndex < 0)
                {
                    continue;
                }

                var key = trimmed[..equalsIndex].Trim();
                if (key.Equals("Authentication", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsAuthenticationConflict(InvalidOperationException exception)
        {
            return exception.Message.IndexOf(
                "AccessToken property",
                StringComparison.OrdinalIgnoreCase) >= 0
                && exception.Message.IndexOf(
                    "'Authentication' has been specified",
                    StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
