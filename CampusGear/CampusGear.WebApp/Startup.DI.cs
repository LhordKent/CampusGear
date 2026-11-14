using CampusGear.Data;
using CampusGear.Data.Interfaces;
using CampusGear.Data.Repositories;
using CampusGear.Services.Interfaces;
using CampusGear.Services.Services;
using CampusGear.Services.Workers;

namespace CampusGear.WebApp
{
    public static partial class StartupConfigurer
    {
        public static IServiceCollection AddCampusGearDependencies(this IServiceCollection services)
        {
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<Microsoft.AspNetCore.Identity.IUserStore<CampusGear.Data.Models.ApplicationUser>, AccountUserStore>();
            services.AddScoped<IWorkflowLockManager, WorkflowLockManager>();
            services.AddScoped<IAuditRepository, AuditRepository>();
            services.AddScoped<ICategoryRepository, CategoryRepository>();
            services.AddScoped<IEquipmentRepository, EquipmentRepository>();
            services.AddScoped<IMaintenanceRepository, MaintenanceRepository>();
            services.AddScoped<IReservationRepository, ReservationRepository>();
            services.AddScoped<IBorrowerProfileRepository, BorrowerProfileRepository>();
            services.AddScoped<IAccountRepository, AccountRepository>();
            services.AddScoped<ILoanRepository, LoanRepository>();
            services.AddScoped<IEmailChallengeRepository, EmailChallengeRepository>();
            services.AddScoped<IBorrowerReadRepository, BorrowerReadRepository>();
            services.AddScoped<IEquipmentAvailabilityRepository, EquipmentAvailabilityRepository>();
            services.AddScoped<IDirectoryReadRepository, DirectoryReadRepository>();
            services.AddScoped<ICustodianReadRepository, CustodianReadRepository>();
            services.AddScoped<IDashboardReadRepository, DashboardReadRepository>();
            services.AddScoped<ICategoryService, CategoryService>();
            services.AddScoped<IInventoryService, InventoryService>();
            services.AddScoped<IMaintenanceService, MaintenanceService>();
            services.AddScoped<IEmailChallengeService, EmailChallengeService>();
            services.AddScoped<IEquipmentAvailabilityService, EquipmentAvailabilityService>();
            services.AddScoped<IEquipmentCatalogService, EquipmentCatalogService>();
            services.AddScoped<IReservationRequestService, ReservationRequestService>();
            services.AddScoped<IBorrowingHistoryService, BorrowingHistoryService>();
            services.AddScoped<IAvailabilityCalendarService, AvailabilityCalendarService>();
            services.AddScoped<IAuditService, AuditService>();
            services.AddScoped<IDirectoryService, DirectoryService>();
            services.AddScoped<ICustodianQueueService, CustodianQueueService>();
            services.AddScoped<IDashboardService, DashboardService>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IAuthenticationService, AuthenticationService>();
            services.AddScoped<IRegistrationService, RegistrationService>();
            services.AddScoped<IPasswordResetService, PasswordResetService>();
            services.AddScoped<ICurrentAccountService, CurrentAccountService>();
            services.AddScoped<IReservationService, ReservationService>();
            services.AddScoped<EmailChallengeService>();
            services.AddScoped<IAccountFlowStore, CampusGear.WebApp.Infrastructure.AccountFlowStore>();
            services.AddSingleton(TimeProvider.System);
            services.AddSingleton<IEquipmentImageStore, EquipmentImageStore>();
            services.AddHostedService<ReservationExpiryWorker>();
            return services;
        }
    }
}
