using Microsoft.Extensions.DependencyInjection;
namespace CampusGear.Services;
public static class DependencyInjection
{
    public static IServiceCollection AddCampusGearServices(this IServiceCollection services)
    {
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IMaintenanceService, MaintenanceService>();
        services.AddScoped<IEmailChallengeService, EmailChallengeService>();
        return services;
    }
}
