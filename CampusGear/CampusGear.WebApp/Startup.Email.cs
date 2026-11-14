using CampusGear.Services.Interfaces;
using CampusGear.Services.Services;

namespace CampusGear.WebApp
{
    public static partial class StartupConfigurer
    {
        public static string AddCampusGearEmail(
            this IServiceCollection services,
            IConfiguration configuration,
            IHostEnvironment environment
        )
        {
            // The in-memory mailbox must be explicitly selected; it never delivers to an inbox.
            var emailProvider = configuration["Email:Provider"] ?? "Smtp";
            if (emailProvider.Equals("Development", StringComparison.OrdinalIgnoreCase))
            {
                if (!environment.IsDevelopment())
                {
                    throw new InvalidOperationException("The Development email provider is available only in Development.");
                }

                services.AddSingleton<DevelopmentEmailSender>();
                services.AddSingleton<IEmailSender>(sp => sp.GetRequiredService<DevelopmentEmailSender>());
            }
            else if (emailProvider.Equals("Smtp", StringComparison.OrdinalIgnoreCase))
            {
                if (environment.IsDevelopment())
                {
                    // Temporary: Development also copies every message to /development/email.
                    services.AddSingleton<DevelopmentEmailSender>();
                    services.AddSingleton<SmtpEmailSender>();
                    services.AddSingleton<IEmailSender, DevelopmentMirrorEmailSender>();
                }
                else
                {
                    services.AddSingleton<IEmailSender, SmtpEmailSender>();
                }
            }
            else
            {
                throw new InvalidOperationException("Email:Provider must be Development or Smtp.");
            }

            return emailProvider;
        }
    }
}
