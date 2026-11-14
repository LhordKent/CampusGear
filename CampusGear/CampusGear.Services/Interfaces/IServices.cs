using CampusGear.Data.Models;
using CampusGear.Data.QueryModels;
using CampusGear.Resources.Constants;
using CampusGear.Services.ServiceModels;

namespace CampusGear.Services.Interfaces
{
    public interface IAuditService
    {
        Task<AdminAuditPage> GetAuditAsync(string? q, int page = 1, CancellationToken cancellationToken = default);
        Task RecordAccountAsync(RequestContext actor, string action, string outcome, string? userId, CancellationToken ct);
    }

    public interface IAuthenticationService
    {
        Task<OperationResult<AuthenticationResult>> GetLoginAsync(RequestContext actor, CancellationToken ct = default);
        Task<OperationResult<AuthenticationResult>> LoginAsync(RequestContext actor, LoginInput input, CancellationToken ct = default);
        Task<OperationResult<AuthenticationResult>> GetTwoFactorAsync(RequestContext actor, CancellationToken ct = default);
        Task<OperationResult<AuthenticationResult>> TwoFactorAsync(RequestContext actor, string? code, CancellationToken ct = default);
        Task<OperationResult<AuthenticationResult>> ResendTwoFactorAsync(RequestContext actor, CancellationToken ct = default);
        Task<OperationResult<AuthenticationResult>> LogoutAsync(RequestContext actor, CancellationToken ct = default);
    }

    public interface IAvailabilityCalendarService
    {
        Task<OperationResult<CalendarPageViewModel>> CalendarAsync(
            RequestContext actor,
            int? year,
            int? month,
            Guid? equipmentItemId,
            string? day,
            CancellationToken cancellationToken
        );
        Task<CustodianCalendarViewModel> GetStaffCalendarAsync(
            int? year,
            int? month,
            Guid? equipmentItemId,
            string? day,
            CancellationToken cancellationToken
        );
    }

    public interface IBorrowingHistoryService
    {
        Task<OperationResult<HistoryPageViewModel>> HistoryAsync(
            RequestContext actor,
            string? status,
            string? from,
            string? to,
            string? q,
            int page = 1,
            CancellationToken cancellationToken = default
        );
    }

    public interface ICategoryService
    {
        Task<CategoryEditModel?> GetEditAsync(Guid id, CancellationToken ct);
        Task<CategoryPage> GetPageAsync(string? query, CancellationToken ct);
        Task<OperationResult<Guid>> SaveAsync(RequestContext actor, CategoryEditModel input, CancellationToken ct);
        Task<OperationResult<Guid>> DeleteAsync(RequestContext actor, Guid id, CancellationToken ct);
        Task<OperationResult<Guid>> AddFromInventoryAsync(RequestContext actor, string? name, CancellationToken ct);
    }

    public interface ICurrentAccountService
    {
        Task<CurrentAccountDisplay> GetAsync(string? userId, string fallback);
    }

    public interface ICustodianQueueService
    {
        Task<CustodianDetailViewModel?> GetDetailsAsync(Guid id, bool custodian, CancellationToken cancellationToken);
        Task<CustodianListViewModel> GetApprovalQueueAsync(CustodianQueueFilter filter, CancellationToken cancellationToken);
        Task<CustodianListViewModel> GetReturnQueueAsync(CustodianQueueFilter filter, CancellationToken cancellationToken);
        Task<CustodianListViewModel> GetHistoryAsync(CustodianQueueFilter filter, CancellationToken cancellationToken);
    }

    public interface IDashboardService
    {
        Task<CustodianDashboardViewModel> GetCustodianAsync(string userId, CancellationToken ct);
        Task<AdminDashboardViewModel> GetAdministratorAsync(string userId, CancellationToken ct);
    }

    public interface IDirectoryService
    {
        Task<IReadOnlyList<DirectoryCategory>> GetCategoriesAsync(CancellationToken ct);
        Task<DirectoryBorrowersPage> GetBorrowersAsync(string? query, string? eligibility, int page, CancellationToken ct);
    }

    public interface IEmailChallengeService
    {
        Task<ChallengeIssueResult> IssueAsync(ApplicationUser user, EmailChallengePurpose purpose, CancellationToken ct = default);
        Task<ChallengeVerifyStatus> VerifyAsync(string userId, EmailChallengePurpose purpose, string? code, CancellationToken ct = default);
    }

    public interface IEquipmentAvailabilityService
    {
        Task<Dictionary<Guid, ItemStatus>> GetStatesAsync(DateTime nowUtc, CancellationToken ct);
        Task<IReadOnlyList<EquipmentPickerItem>> GetPickerAsync(DateTime nowUtc, CancellationToken ct);
        Task<List<Guid>> FindFreeUnitsAsync(
            IReadOnlyList<Guid> unitIds,
            DateTime startUtc,
            DateTime endUtc,
            DateTime now,
            Guid preferredUnitId,
            CancellationToken cancellationToken
        );
    }

    public interface IEquipmentCatalogService
    {
        Task<OperationResult<BorrowerHomeViewModel>> HomeAsync(RequestContext actor, CancellationToken cancellationToken);
        Task<OperationResult<ReservationPageViewModel>> ReservationAsync(
            RequestContext actor,
            Guid? equipmentItemId,
            string? state,
            Guid[]? id,
            CancellationToken cancellationToken
        );
        Task<OperationResult<AvailabilityResult>> AvailabilityAsync(
            RequestContext actor,
            Guid equipmentItemId,
            string? start,
            string? end,
            CancellationToken cancellationToken
        );
        Task<ReservationPageViewModel> GetRequestPageAsync(
            bool isEligible,
            ReservationRequestModel input,
            CancellationToken cancellationToken
        );
    }

    public interface IInventoryService
    {
        Task<EquipmentRecord?> GetEditAsync(Guid id, CancellationToken ct);
        Task<InventoryPage> GetPageAsync(string? query, int page, CancellationToken ct);
        Task<OperationResult<Guid>> SaveAsync(
            RequestContext actor,
            EquipmentEditModel input,
            UploadContent? imageFile,
            CancellationToken ct
        );
        Task<OperationResult<Guid>> DeleteAsync(RequestContext actor, Guid id, CancellationToken ct);
    }

    public interface IMaintenanceService
    {
        Task<MaintenancePage> GetPageAsync(string? status, string? query, int page, CancellationToken ct);
        Task<OperationResult<bool>> OpenAsync(RequestContext actor, Guid equipmentItemId, string? description, CancellationToken ct);
        Task<OperationResult<bool>> UpdateAsync(RequestContext actor, Guid id, MaintenanceEditModel input, CancellationToken ct);
    }

    public interface IPasswordResetService
    {
        Task<OperationResult<PasswordResetResult>> PasswordResetRequestAsync(
            RequestContext actor,
            string emailInput,
            CancellationToken ct = default
        );
        Task<OperationResult<PasswordResetResult>> GetPasswordResetCodeAsync(RequestContext actor, CancellationToken ct = default);
        Task<OperationResult<PasswordResetResult>> PasswordResetCodeAsync(
            RequestContext actor,
            string? code,
            CancellationToken ct = default
        );
        Task<OperationResult<PasswordResetResult>> ResendPasswordResetCodeAsync(RequestContext actor, CancellationToken ct = default);
        Task<OperationResult<PasswordResetResult>> GetPasswordResetNewAsync(RequestContext actor, CancellationToken ct = default);
        Task<OperationResult<PasswordResetResult>> PasswordResetNewAsync(
            RequestContext actor,
            string? password,
            CancellationToken ct = default
        );
    }

    public interface IRegistrationService
    {
        Task<OperationResult<RegistrationResult>> SignUpAsync(RequestContext actor, SignUpInput input, CancellationToken ct = default);
    }

    public interface IReservationRequestService
    {
        Task<OperationResult<ReservationPageViewModel>> CreateReservationAsync(
            RequestContext actor,
            ReservationRequestModel input,
            CancellationToken cancellationToken
        );
    }

    public interface IReservationService
    {
        Task<ReservationResult> CreateAsync(
            ReservationActor actor,
            CreateReservationCommand command,
            CancellationToken cancellationToken = default
        );
        Task<ReservationResult> ApproveAsync(ReservationActor actor, Guid reservationId, CancellationToken cancellationToken = default);
        Task<ReservationResult> RejectAsync(
            ReservationActor actor,
            Guid reservationId,
            string reason,
            CancellationToken cancellationToken = default
        );
        Task<ReservationResult> CancelAsync(
            ReservationActor actor,
            Guid reservationId,
            string? reason = null,
            CancellationToken cancellationToken = default
        );
        Task<ReservationResult> ReleaseAsync(
            ReservationActor actor,
            Guid reservationId,
            EquipmentCondition conditionAtRelease,
            string? notes = null,
            CancellationToken cancellationToken = default
        );
        Task<ReservationResult> ReturnAsync(
            ReservationActor actor,
            Guid reservationId,
            EquipmentCondition conditionAtReturn,
            string? notes = null,
            CancellationToken cancellationToken = default
        );
        Task<int> ExpireDueAsync(int batchSize = 100, CancellationToken cancellationToken = default);
    }

    public interface IUserService
    {
        Task<AdminUsersPage> GetPageAsync(
            string? q,
            string? role,
            string? status,
            int page = 1,
            CancellationToken cancellationToken = default
        );
        Task<OperationResult<string>> DecideAsync(
            RequestContext actor,
            string id,
            AccountApprovalStatus decision,
            CancellationToken cancellationToken
        );
        Task<OperationResult<string>> EditAsync(RequestContext actor, string id, UserEditModel input, CancellationToken cancellationToken);
        Task<OperationResult<string>> CreateAsync(RequestContext actor, UserCreateModel input, CancellationToken cancellationToken);
        Task<OperationResult<string>> DeleteAsync(RequestContext actor, string id, CancellationToken cancellationToken);
        Task<UserEditPage?> GetEditAsync(RequestContext actor, string id, UserEditModel? input, CancellationToken cancellationToken);
    }
}
