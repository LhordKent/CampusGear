using Microsoft.Extensions.DependencyInjection;
namespace CampusGear.Services;
public static class DependencyInjection
{
    public static IServiceCollection AddCampusGearServices(this IServiceCollection services)
    {
        services.AddScoped<ICategoryService, CategoryService>();
        return services;
    }
}
