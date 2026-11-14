using CampusGear.Data.Models;
using CampusGear.Data.QueryModels;
using CampusGear.Resources.Constants;

namespace CampusGear.Data.Interfaces
{
    public interface IAccountRepository
    {
        Task<bool> HasBorrowerRoleAsync(string userId, CancellationToken ct);
        Task<AccountPageData> ListAsync(string search, string role, string status, int page, CancellationToken ct);
        Task<ApplicationUser?> FindAsync(string id, CancellationToken ct);
        Task<ApplicationUser?> FindFreshAsync(string id, CancellationToken ct);
        Task<ApplicationUser?> FindWithProfileAsync(string id, CancellationToken ct);
        Task<ApplicationUser?> FindWithProfileFreshAsync(string id, CancellationToken ct);
        Task<bool> StudentExistsAsync(string number, string? excludingUser, CancellationToken ct);
        Task<bool> HasOtherAdministratorAsync(string excludingUser, CancellationToken ct);
        Task<bool> HasHistoryAsync(string userId, CancellationToken ct);
        Task PrepareDeletionAsync(ApplicationUser user, CancellationToken ct);
        Task ReloadProfileAsync(BorrowerProfile profile, CancellationToken ct);
        Task<bool> ReloadUserAsync(ApplicationUser user, CancellationToken ct);
        void AddProfile(BorrowerProfile profile);
    }

    public interface IAuditRepository
    {
        void Add(AuditEvent record);
        Task<AuditPageData> ListAsync(string search, int page, CancellationToken ct);
    }

    public interface IBorrowerProfileRepository
    {
        Task<Guid?> GetIdAsync(string userId, CancellationToken ct);
        Task<BorrowerProfile?> FindFreshAsync(Guid id, CancellationToken ct);
    }

    public interface IBorrowerReadRepository
    {
        Task<BorrowerProfile?> FindProfileAsync(string? userId, CancellationToken ct);
        Task<IReadOnlyList<CatalogUnit>> CatalogAsync(CancellationToken ct);
        Task<IReadOnlyList<Guid>> InUseAsync(DateTime now, CancellationToken ct);
        Task<IReadOnlyList<RequestRecord>> FindRequestsAsync(Guid profileId, Guid[] ids, CancellationToken ct);
        Task<IReadOnlyList<Reservation>> SubmittedAsync(Guid profileId, IReadOnlyList<Guid> ids, CancellationToken ct);
        Task<BorrowerHomeData> HomeAsync(Guid profileId, DateTime now, CancellationToken ct);
        Task<ReservationRows> HistoryAsync(
            Guid profileId,
            string status,
            string? query,
            DateTime? fromUtc,
            DateTime? toUtc,
            DateTime now,
            int page,
            CancellationToken ct
        );
    }

    public interface ICategoryRepository
    {
        Task<EquipmentCategory?> FindAsync(Guid id, CancellationToken cancellationToken);
        Task<CategoryList> ListAsync(string? query, CancellationToken cancellationToken);
        Task<bool> HasItemsAsync(Guid id, bool activeOnly, CancellationToken cancellationToken);
        Task<bool> IsActiveAsync(Guid id, CancellationToken cancellationToken);
        Task<IReadOnlyList<EquipmentCategory>> ActiveAsync(CancellationToken cancellationToken);
        void Add(EquipmentCategory category);
        void Remove(EquipmentCategory category);
    }

    public interface ICustodianReadRepository
    {
        Task<Reservation?> DetailsAsync(Guid id, CancellationToken ct);
        Task<CustodianRows> ListAsync(
            string queue,
            string status,
            string? search,
            DateTime? from,
            DateTime? to,
            DateTime now,
            int page,
            CancellationToken ct
        );
    }

    public interface IDashboardReadRepository
    {
        Task<DeskData> DeskAsync(DateTime now, DateTime todayEndUtc, CancellationToken ct);
        Task<IReadOnlyList<Reservation>> PendingRequestsAsync(DateTime now, CancellationToken ct);
        Task<IReadOnlyList<Loan>> DueLoansAsync(DateTime todayEndUtc, CancellationToken ct);
        Task<int> PendingRegistrationsAsync(CancellationToken ct);
        Task<IReadOnlyList<Reservation>> RecentReservationsAsync(CancellationToken ct);
        Task<IReadOnlyList<ApplicationUser>> RegistrationsPendingAsync(CancellationToken ct);
        Task<int> OpenCasesAsync(CancellationToken ct);
        Task<IReadOnlyList<ValueCount<EquipmentCondition>>> ConditionsAsync(CancellationToken ct);
        Task<IReadOnlyList<ValueCount<ReservationStatus>>> StatusesAsync(CancellationToken ct);
        Task<int> LivePendingAsync(DateTime now, CancellationToken ct);
        Task<IReadOnlyList<DateTime>> RequestTimesAsync(DateTime fromUtc, CancellationToken ct);
        Task<IReadOnlyList<DateTime>> ReleaseTimesAsync(DateTime fromUtc, CancellationToken ct);
        Task<IReadOnlyList<DateTime>> ReturnTimesAsync(DateTime fromUtc, CancellationToken ct);
        Task<IReadOnlyList<DateTime>> RegistrationTimesAsync(DateTime fromUtc, CancellationToken ct);
        Task<IReadOnlyList<Loan>> RecentLoansAsync(CancellationToken ct);
    }

    public interface IDirectoryReadRepository
    {
        Task<IReadOnlyList<DirectoryCategoryData>> CategoriesAsync(CancellationToken ct);
        Task<DirectoryPageData> BorrowersAsync(string query, string eligibility, int page, CancellationToken ct);
    }

    public interface IEmailChallengeRepository
    {
        Task<IReadOnlyList<EmailChallenge>> UnconsumedAsync(string userId, EmailChallengePurpose purpose, CancellationToken ct);
        Task<EmailChallenge?> LatestValidAsync(string userId, EmailChallengePurpose purpose, CancellationToken ct);
        void Add(EmailChallenge challenge);
        void DetachWrites();
    }

    public interface IEquipmentAvailabilityRepository
    {
        Task<IReadOnlyList<Guid>> BlockedAsync(IReadOnlyList<Guid> ids, DateTime start, DateTime end, DateTime now, CancellationToken ct);
        Task<IReadOnlyList<CalendarItemData>> CalendarItemsAsync(CancellationToken ct);
        Task<IReadOnlyList<Reservation>> CalendarBookingsAsync(
            Guid itemId,
            DateTime fromUtc,
            DateTime toUtc,
            DateTime now,
            bool staff,
            CancellationToken ct
        );
        Task<IReadOnlyList<EquipmentStateData>> EquipmentStatesAsync(DateTime now, CancellationToken ct);
    }

    public interface IEquipmentRepository
    {
        Task<EquipmentItem?> FindAsync(Guid id, CancellationToken ct);
        Task<bool> HasHistoryAsync(Guid id, CancellationToken ct);
        Task<bool> HasOutstandingLoanAsync(Guid id, CancellationToken ct);
        Task<bool> HasLiveReservationsAsync(Guid id, DateTime now, CancellationToken ct);
        Task<InventoryData> InventoryAsync(string? query, int page, CancellationToken ct);
        void Add(EquipmentItem item);
        void Remove(EquipmentItem item);
        void MarkUpdated(EquipmentItem item);
    }

    public interface ILoanRepository
    {
        Task<bool> HasOutstandingExceptAsync(Guid itemId, Guid reservationId, CancellationToken ct);
        void Add(Loan loan);
    }

    public interface IMaintenanceRepository
    {
        Task<bool> HasOpenAsync(Guid itemId, CancellationToken ct, Guid? excludingCase = null);
        void Add(MaintenanceCase item);
        Task<Guid?> GetItemIdAsync(Guid id, CancellationToken ct);
        Task<MaintenanceCase> FindWithItemAsync(Guid id, CancellationToken ct);
        Task<MaintenanceListData> ListAsync(string status, string? query, int page, CancellationToken ct);
    }

    public sealed record ExpiryCandidate(Guid Id, Guid EquipmentItemId);

    public interface IReservationRepository
    {
        Task<Reservation?> FindByRequestIdAsync(Guid requestId, CancellationToken ct);
        Task<Reservation?> FindAsync(Guid id, CancellationToken ct);
        Task<Reservation?> FindWorkflowAsync(Guid id, CancellationToken ct);
        Task<Guid?> GetItemIdAsync(Guid id, CancellationToken ct);
        Task<bool> HasConflictAsync(Guid itemId, DateTime start, DateTime end, DateTime now, Guid? excluding, CancellationToken ct);
        Task<IReadOnlyList<ExpiryCandidate>> ExpiryCandidatesAsync(DateTime now, int size, CancellationToken ct);
        void Add(Reservation reservation);
    }
}
