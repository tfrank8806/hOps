#nullable enable

using System;
using Microsoft.Extensions.Configuration;

namespace hOps.web.Options
{
    public class SupabaseStorageOptions
    {
        public string? ProjectUrl { get; set; }
        public string? StorageBucket { get; set; }
        public string? ServiceRoleKey { get; set; }

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(ProjectUrl) &&
            !string.IsNullOrWhiteSpace(StorageBucket) &&
            !string.IsNullOrWhiteSpace(ServiceRoleKey);

        public static SupabaseStorageOptions FromConfiguration(IConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            var options = new SupabaseStorageOptions
            {
                ProjectUrl = configuration["Supabase:ProjectUrl"],
                StorageBucket = configuration["Supabase:StorageBucket"],
                ServiceRoleKey = configuration["Supabase:ServiceRole"]
            };

            var envServiceRole = Environment.GetEnvironmentVariable("SUPABASE_SERVICE_ROLE");
            if (!string.IsNullOrWhiteSpace(envServiceRole))
            {
                options.ServiceRoleKey = envServiceRole;
            }

            return options;
        }
    }
}
