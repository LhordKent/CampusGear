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
        services.AddScoped<IEquipmentAvailabilityService, EquipmentAvailabilityService>();
        services.AddScoped<IBorrowerService, BorrowerService>();
        services.AddScoped<IDirectoryService, DirectoryService>();
        services.AddScoped<ICustodianService, CustodianService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IUserAdministrationService, UserAdministrationService>();
        return services;
    }
}
