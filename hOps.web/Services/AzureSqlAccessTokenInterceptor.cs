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
            if (connection is SqlConnection sqlConnection)
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
            if (connection is SqlConnection sqlConnection)
            {
                EnsureAccessTokenAsync(sqlConnection, CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
            }

            return base.ConnectionOpening(connection, eventData, result);
        }

        private async Task EnsureAccessTokenAsync(SqlConnection sqlConnection, CancellationToken cancellationToken)
        {
            if (!RequiresAccessToken(sqlConnection.ConnectionString))
            {
                return;
            }

            var token = await _credential.GetTokenAsync(
                new TokenRequestContext(SqlScopes),
                cancellationToken);

            sqlConnection.AccessToken = token.Token;
        }

        private static bool RequiresAccessToken(string? connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return false;
            }

            if (ContainsAuthenticationKeyword(connectionString))
            {
                // An explicit Authentication clause means SqlClient will handle token acquisition.
                return false;
            }

            try
            {
                var builder = new SqlConnectionStringBuilder(connectionString);

                if (builder.Authentication is not SqlAuthenticationMethod.NotSpecified)
                {
                    return false;
                }

                if (!string.IsNullOrEmpty(builder.UserID)
                    && string.IsNullOrEmpty(builder.Password)
                    && builder.DataSource?.Contains("database.windows.net", StringComparison.OrdinalIgnoreCase) == true)
                {
                    return true;
                }
            }
            catch
            {
                // Ignore invalid connection strings; we only care about obvious Azure SQL cases.
            }

            return false;
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
    }
}
