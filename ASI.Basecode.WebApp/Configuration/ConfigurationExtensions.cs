using Microsoft.Extensions.FileProviders;

namespace ASI.Basecode.WebApp.Configuration
{
    /// <summary>
    /// Configuration helpers used while the application starts.
    /// </summary>
    public static class ConfigurationExtensions
    {
        public sealed record SecretsLoadStatus(bool FileFound, bool HostLoaded, bool FromLoaded);

        /// <summary>The SQL Server connection string (ConnectionStrings:CampusGear).</summary>
        public static string GetCampusGearConnectionString(this IConfiguration configuration)
        {
            return configuration.GetConnectionString("CampusGear")
                ?? throw new InvalidOperationException("ConnectionStrings:CampusGear is required.");
        }

        /// <summary>The selected email provider (Email:Provider): Smtp unless Development is chosen explicitly.</summary>
        public static string GetEmailProvider(this IConfiguration configuration)
        {
            return configuration["Email:Provider"] ?? "Smtp";
        }

        /// <summary>
        /// Development only: loads the shared %USERPROFILE%\.campusgear\email-secrets.json file, then the project
        /// User Secrets, environment variables and command line, so the later sources win.
        /// </summary>
        public static SecretsLoadStatus LoadProjectSecrets(this ConfigurationManager configuration, IHostEnvironment environment, string[] args)
        {
            if (!environment.IsDevelopment())
            {
                return new(false, false, false);
            }

            // AppData from packaged desktop tools is private to that package. The
            // user profile is shared with Visual Studio and ordinary shells.
            var sharedFile =
                configuration["Email:DevelopmentSettingsFile"]
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".campusgear", "email-secrets.json");
            var fileFound = File.Exists(sharedFile);
            if (fileFound)
            {
                configuration.AddJsonFile(
                    new PhysicalFileProvider(Path.GetDirectoryName(Path.GetFullPath(sharedFile))!),
                    Path.GetFileName(sharedFile),
                    optional: false,
                    reloadOnChange: true
                );
            }

            // Standard User Secrets and explicit overrides take precedence.
            configuration.AddUserSecrets(typeof(ConfigurationExtensions).Assembly, optional: true, reloadOnChange: true);
            configuration.AddEnvironmentVariables();
            if (args.Length > 0)
            {
                configuration.AddCommandLine(args);
            }

            return new(
                fileFound,
                !string.IsNullOrWhiteSpace(configuration["Email:Smtp:Host"]),
                !string.IsNullOrWhiteSpace(configuration["Email:Smtp:From"])
            );
        }
    }
}
