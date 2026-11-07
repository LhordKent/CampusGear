using CampusGear.Data.Interfaces;
using CampusGear.Data.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace CampusGear.Data;

public static class DependencyInjection
{
    /// <summary>Does not create a second DbContext: repositories use the host's existing scoped registration.</summary>
    public static IServiceCollection AddCampusGearData(this IServiceCollection services)
    {
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IWorkflowLockManager, WorkflowLockManager>();
        services.AddScoped<IAuditRepository, AuditRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IEquipmentRepository, EquipmentRepository>();
        services.AddScoped<IMaintenanceRepository, MaintenanceRepository>();
        return services;
    }
}
