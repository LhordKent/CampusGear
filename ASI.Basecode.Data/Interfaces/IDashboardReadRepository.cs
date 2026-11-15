using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;

namespace ASI.Basecode.Data.Interfaces
{
    public interface IDashboardReadRepository
    {
        Task<DeskData> DeskAsync(DateTime now, DateTime todayEndUtc, CancellationToken ct);
        Task<IReadOnlyList<Reservation>> PendingRequestsAsync(DateTime now, CancellationToken ct);
        Task<IReadOnlyList<Loan>> DueLoansAsync(DateTime todayEndUtc, CancellationToken ct);
        Task<int> PendingRegistrationsAsync(CancellationToken ct);
        Task<IReadOnlyList<Reservation>> RecentReservationsAsync(CancellationToken ct);
        Task<IReadOnlyList<User>> RegistrationsPendingAsync(CancellationToken ct);
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
}
