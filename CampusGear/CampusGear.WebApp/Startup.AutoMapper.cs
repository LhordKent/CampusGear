using AutoMapper;
using CampusGear.Services.Mapping;

namespace CampusGear.WebApp
{
    public static partial class StartupConfigurer
    {
        public static IServiceCollection AddCampusGearMapping(this IServiceCollection services)
        {
            services.AddSingleton<IMapper>(provider =>
            {
                var configuration = new MapperConfiguration(
                    options =>
                    {
                        options.AddProfile<ServiceMappingProfile>();
                        // Supply an operator-owned key through configuration when using a
                        // commercial license. Never commit keys or suppress license diagnostics.
                        options.LicenseKey = provider.GetRequiredService<IConfiguration>()["AutoMapper:LicenseKey"];
                    },
                    provider.GetRequiredService<ILoggerFactory>()
                );
                configuration.AssertConfigurationIsValid();
                return configuration.CreateMapper();
            });
            return services;
        }
    }
}
