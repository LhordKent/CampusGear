using ASI.Basecode.Data;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Repositories;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Manager;
using ASI.Basecode.Services.Services;
using ASI.Basecode.WebApp.Configuration;
using Microsoft.AspNetCore.Identity;

// =====================================================================
// DEPENDENCY INJECTION  -  the list that says "when a class asks for IXxx, give it Xxx".
// Example: a controller asks for IReservationService -> it gets ReservationService.
// Looking for which class really runs behind an interface? Search for it in this file.
// "Scoped" = one instance per web request.  "Singleton" = one instance for the whole app.
// =====================================================================
namespace ASI.Basecode.WebApp
{
    // Other services configuration
    internal partial class StartupConfigurer
    {
        private string _emailProvider = "Smtp";

        private void ConfigureOtherServices()
        {
            // Common
            this._services.AddScoped<IUnitOfWork, UnitOfWork>();
            this._services.AddSingleton(TimeProvider.System);

            // Repositories
            // Registered before Identity so Identity uses CampusGear's user store.
            this._services.AddScoped<IUserStore<User>, AccountUserStore>();
            this._services.AddScoped<IWorkflowLockManager, WorkflowLockManager>();
            this._services.AddScoped<IAuditRepository, AuditRepository>();
            this._services.AddScoped<ICategoryRepository, CategoryRepository>();
            this._services.AddScoped<IAssetCodeRepository, AssetCodeRepository>();
            this._services.AddScoped<IEquipmentRepository, EquipmentRepository>();
            this._services.AddScoped<IMaintenanceRepository, MaintenanceRepository>();
            this._services.AddScoped<IReservationRepository, ReservationRepository>();
            this._services.AddScoped<IBorrowerProfileRepository, BorrowerProfileRepository>();
            this._services.AddScoped<IUserRepository, UserRepository>();
            this._services.AddScoped<ILoanRepository, LoanRepository>();
            this._services.AddScoped<IEmailChallengeRepository, EmailChallengeRepository>();
            this._services.AddScoped<IBorrowerReadRepository, BorrowerReadRepository>();
            this._services.AddScoped<IEquipmentAvailabilityRepository, EquipmentAvailabilityRepository>();
            this._services.AddScoped<IDirectoryReadRepository, DirectoryReadRepository>();
            this._services.AddScoped<ICustodianReadRepository, CustodianReadRepository>();
            this._services.AddScoped<IDashboardReadRepository, DashboardReadRepository>();

            // Services
            this._services.AddScoped<ICategoryService, CategoryService>();
            this._services.AddScoped<IInventoryService, InventoryService>();
            this._services.AddScoped<IMaintenanceService, MaintenanceService>();
            this._services.AddScoped<IEmailChallengeService, EmailChallengeService>();
            this._services.AddScoped<IEquipmentAvailabilityService, EquipmentAvailabilityService>();
            this._services.AddScoped<IEquipmentCatalogService, EquipmentCatalogService>();
            this._services.AddScoped<IReservationRequestService, ReservationRequestService>();
            this._services.AddScoped<IBorrowingHistoryService, BorrowingHistoryService>();
            this._services.AddScoped<IAvailabilityCalendarService, AvailabilityCalendarService>();
            this._services.AddScoped<IAuditService, AuditService>();
            this._services.AddScoped<IDirectoryService, DirectoryService>();
            this._services.AddScoped<ICustodianQueueService, CustodianQueueService>();
            this._services.AddScoped<IDashboardService, DashboardService>();
            this._services.AddScoped<IUserService, UserService>();
            // WBS 4.00 / WBS 5.00 | Route account actions to the Identity-backed authentication service.
            this._services.AddScoped<IAuthenticationService, AuthenticationService>();
            this._services.AddScoped<IRegistrationService, RegistrationService>();
            this._services.AddScoped<IPasswordResetService, PasswordResetService>();
            this._services.AddScoped<ICurrentAccountService, CurrentAccountService>();
            // WBS 25.00 / WBS 28.00 | Borrower submissions and custodian decisions share the reservation workflow.
            this._services.AddScoped<IReservationService, ReservationService>();
            this._services.AddScoped<EmailChallengeService>();
            this._services.AddSingleton<IEquipmentImageStore, EquipmentImageStore>();
            this._services.AddHostedService<ReservationExpiryWorker>();

            // Manager Class
            this._services.AddScoped<IAccountFlowStore, SessionManager>();
        }

        private void ConfigureEmail()
        {
            // The in-memory mailbox must be explicitly selected; it never delivers to an inbox.
            this._emailProvider = this.Configuration.GetEmailProvider();
            if (this._emailProvider.Equals("Development", StringComparison.OrdinalIgnoreCase))
            {
                if (!this._environment.IsDevelopment())
                {
                    throw new InvalidOperationException("The Development email provider is available only in Development.");
                }

                this._services.AddSingleton<DevelopmentEmailSender>();
                this._services.AddSingleton<IEmailSender>(sp => sp.GetRequiredService<DevelopmentEmailSender>());
            }
            else if (this._emailProvider.Equals("Smtp", StringComparison.OrdinalIgnoreCase))
            {
                if (this._environment.IsDevelopment())
                {
                    // Temporary: Development also copies every message to /development/email.
                    this._services.AddSingleton<DevelopmentEmailSender>();
                    this._services.AddSingleton<SmtpEmailSender>();
                    this._services.AddSingleton<IEmailSender, DevelopmentMirrorEmailSender>();
                }
                else
                {
                    this._services.AddSingleton<IEmailSender, SmtpEmailSender>();
                }
            }
            else
            {
                throw new InvalidOperationException("Email:Provider must be Development or Smtp.");
            }
        }
    }
}
