using AutoMapper;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.WebApp
{
    // AutoMapper configuration
    internal partial class StartupConfigurer
    {
        /// <summary>
        /// Configure auto mapper
        /// </summary>
        private void ConfigureAutoMapper()
        {
            this._services.AddSingleton<IMapper>(provider =>
            {
                var mapperConfiguration = new MapperConfiguration(
                    config =>
                    {
                        config.AddProfile(new AutoMapperProfileConfiguration());
                        // Supply an operator-owned key through configuration when using a
                        // commercial license. Never commit keys or suppress license diagnostics.
                        config.LicenseKey = this.Configuration["AutoMapper:LicenseKey"];
                    },
                    provider.GetRequiredService<ILoggerFactory>()
                );
                mapperConfiguration.AssertConfigurationIsValid();
                return mapperConfiguration.CreateMapper();
            });
        }

        /// <summary>Read-model mappings only. Identity and state-transition writes are explicit.</summary>
        private class AutoMapperProfileConfiguration : Profile
        {
            public AutoMapperProfileConfiguration()
            {
                CreateMap<CategoryListRecord, CategoryListItem>();
            }
        }
    }
}
