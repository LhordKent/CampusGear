using Microsoft.Extensions.FileProviders;

namespace CampusGear.WebApp.Configuration;

public static class DevelopmentConfiguration
{
    public sealed record SecretsLoadStatus(bool FileFound, bool HostLoaded, bool FromLoaded);

    public static SecretsLoadStatus LoadProjectSecrets(ConfigurationManager configuration, IHostEnvironment environment, string[] args)
    {
        if (!environment.IsDevelopment()) return new(false, false, false);

        // AppData from packaged desktop tools is private to that package. The
        // user profile is shared with Visual Studio and ordinary shells.
        var sharedFile = configuration["Email:DevelopmentSettingsFile"]
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".campusgear", "email-secrets.json");
        var fileFound = File.Exists(sharedFile);
        if (fileFound)
            configuration.AddJsonFile(new PhysicalFileProvider(Path.GetDirectoryName(Path.GetFullPath(sharedFile))!),
                Path.GetFileName(sharedFile), optional: false, reloadOnChange: true);

        // Standard User Secrets and explicit overrides take precedence.
        configuration.AddUserSecrets(typeof(DevelopmentConfiguration).Assembly, optional: true, reloadOnChange: true);
        configuration.AddEnvironmentVariables();
        if (args.Length > 0) configuration.AddCommandLine(args);
        return new(fileFound,
            !string.IsNullOrWhiteSpace(configuration["Email:Smtp:Host"]),
            !string.IsNullOrWhiteSpace(configuration["Email:Smtp:From"]));
    }
}
