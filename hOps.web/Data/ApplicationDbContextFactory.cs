using Azure.Identity;
using hOps.web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace hOps.web.Data
{
    /// <summary>
    /// Provides a design-time DbContext for EF Core tools so migrations target SQL Server even when
    /// the runtime configuration might prefer SQLite for local development.
    /// </summary>
    public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext(string[] args)
        {
            var config = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();

            var connectionString =
                config["ConnectionStrings:DefaultConnection"]
                ?? config["ConnectionStrings__DefaultConnection"]
                ?? "Server=(localdb)\\mssqllocaldb;Database=hOps.web_designtime;Trusted_Connection=True;TrustServerCertificate=True;";

            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            var credentialOptions = new DefaultAzureCredentialOptions
            {
                ExcludeSharedTokenCacheCredential = false,
                ExcludeInteractiveBrowserCredential = false
            };

            optionsBuilder
                .UseSqlServer(connectionString)
                .AddInterceptors(new AzureSqlAccessTokenInterceptor(new DefaultAzureCredential(credentialOptions)));

            return new ApplicationDbContext(optionsBuilder.Options);
        }
    }
}
