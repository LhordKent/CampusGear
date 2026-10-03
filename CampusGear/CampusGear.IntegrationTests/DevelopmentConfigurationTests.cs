using CampusGear.WebApp.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

namespace CampusGear.IntegrationTests;

[Collection("SQL reservation lifecycle")]
public sealed class DevelopmentConfigurationTests
{
    [Theory]
    [InlineData("Development", false, false, false, "shared.example.test")]
    [InlineData("Development", true, false, false, "saved.example.test")]
    [InlineData("Development", false, true, false, "environment.example.test")]
    [InlineData("Development", true, true, false, "environment.example.test")]
    [InlineData("Development", true, true, true, "commandline.example.test")]
    [InlineData("Production", true, false, false, null)]
    public void Project_secrets_load_independently_of_host_assembly_and_keep_override_precedence(
        string environment, bool useProjectSecrets, bool useEnvironment, bool useCommandLine, string? expectedHost)
    {
        var originalAppData = Environment.GetEnvironmentVariable("APPDATA");
        var originalHost = Environment.GetEnvironmentVariable("Email__Smtp__Host");
        var originalColonHost = Environment.GetEnvironmentVariable("Email:Smtp:Host");
        var root = Path.Combine(Path.GetTempPath(), "CampusGear_ConfigTest_" + Guid.NewGuid().ToString("N"));
        try
        {
            var secretsDirectory = Path.Combine(root, "Microsoft", "UserSecrets", "CampusGear-d27882c0-7d71-4d0d-97da-548b14740355");
            Directory.CreateDirectory(secretsDirectory);
            if (useProjectSecrets)
                File.WriteAllText(Path.Combine(secretsDirectory, "secrets.json"), "{\"Email:Smtp:Host\":\"saved.example.test\"}");
            var sharedFile = Path.Combine(root, "email-secrets.json");
            File.WriteAllText(sharedFile, "{\"Email:Smtp:Host\":\"shared.example.test\"}");
            Environment.SetEnvironmentVariable("APPDATA", root);
            Environment.SetEnvironmentVariable("Email__Smtp__Host", useEnvironment ? "environment.example.test" : null);
            Environment.SetEnvironmentVariable("Email:Smtp:Host", null);
            var args = useCommandLine ? new[] { "--Email:Smtp:Host=commandline.example.test" } : Array.Empty<string>();
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                // This assembly has no UserSecretsId; it reproduces absent automatic project secrets.
                ApplicationName = typeof(DevelopmentConfigurationTests).Assembly.GetName().Name,
                EnvironmentName = environment, ContentRootPath = root, Args = args
            });
            if (!useEnvironment && !useCommandLine) Assert.Null(builder.Configuration["Email:Smtp:Host"]);
            builder.Configuration["Email:DevelopmentSettingsFile"] = sharedFile;
            var status = DevelopmentConfiguration.LoadProjectSecrets(builder.Configuration, builder.Environment, args);
            Assert.Equal(environment == "Development", status.FileFound);
            Assert.Equal(expectedHost, builder.Configuration["Email:Smtp:Host"]);
            ((IDisposable)builder.Configuration).Dispose();
        }
        finally
        {
            Environment.SetEnvironmentVariable("APPDATA", originalAppData);
            Environment.SetEnvironmentVariable("Email__Smtp__Host", originalHost);
            Environment.SetEnvironmentVariable("Email:Smtp:Host", originalColonHost);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
