// STARTUP LOG  -  writes a few lines at startup: which email provider is used and whether its settings exist.
namespace ASI.Basecode.WebApp
{
    // Logger configuration
    internal partial class StartupConfigurer
    {
        /// <summary>
        /// Writes the startup diagnostics: which email provider is active and whether its settings were found.
        /// Passwords and codes are never logged.
        /// </summary>
        private void ConfigureLogger()
        {
            var logger = this._app.ApplicationServices.GetRequiredService<ILoggerFactory>().CreateLogger(this._environment.ApplicationName);

            logger.LogInformation("Email provider: {EmailProvider}; environment: {Environment}", this._emailProvider, this._environment.EnvironmentName);
            if (this._emailProvider.Equals("Smtp", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInformation(
                    "SMTP startup configuration: Host configured={HostConfigured}; From configured={FromConfigured}.",
                    !string.IsNullOrWhiteSpace(this.Configuration["Email:Smtp:Host"]),
                    !string.IsNullOrWhiteSpace(this.Configuration["Email:Smtp:From"])
                );
            }

            if (this._environment.IsDevelopment())
            {
                logger.LogInformation("Shared Development email settings: file found={FileFound}.", this._developmentSecrets.FileFound);
            }

            if (this._emailProvider.Equals("Development", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning(
                    "The Development email provider stores messages in memory and does not send email. Select Smtp for inbox delivery."
                );
            }
        }
    }
}
