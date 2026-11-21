using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using Microsoft.EntityFrameworkCore;

// =====================================================================
// RESERVATION REPOSITORY  -  database queries for bookings (used by ReservationService).
// =====================================================================
namespace ASI.Basecode.Data.Repositories
{
    public sealed class ReservationRepository : BaseRepository, IReservationRepository
    {
        public ReservationRepository(IUnitOfWork unitOfWork, AsiBasecodeDBContext db)
            : base(unitOfWork, db) { }

        // Finds a booking by the form's request id (to catch the same form being submitted twice).
        public Task<Reservation?> FindByRequestIdAsync(Guid requestId, CancellationToken ct)
        {
            return Context
                .Reservations.AsNoTracking()
                .Include(x => x.BorrowerProfile)
                .Include(x => x.Loan)
                .SingleOrDefaultAsync(x => x.RequestId == requestId, ct);
        }

        public Task<Reservation?> FindAsync(Guid id, CancellationToken ct)
        {
            return Context.Reservations.SingleOrDefaultAsync(x => x.Id == id, ct);
        }

        // WBS 28.00 | Load the tracked request and related records the service needs for the decision.
        // The workflow load includes the loan, equipment item and borrower profile.
        public Task<Reservation?> FindWorkflowAsync(Guid id, CancellationToken ct)
        {
            return Context
                .Reservations.Include(x => x.Loan)
                .Include(x => x.EquipmentItem)
                .Include(x => x.BorrowerProfile)
                .SingleOrDefaultAsync(x => x.Id == id, ct);
        }

        // Only the item id of a booking (so the item can be locked before the booking is loaded).
        public Task<Guid?> GetItemIdAsync(Guid id, CancellationToken ct)
        {
            return Context
                .Reservations.AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => (Guid?)x.EquipmentItemId)
                .SingleOrDefaultAsync(ct);
        }

        // WBS 25.00 | Strict time comparisons allow adjacent bookings; the optional ID excludes the request being approved.
        // Approved, released and still-held pending bookings can block the time range.
        public Task<bool> HasConflictAsync(Guid itemId, DateTime start, DateTime end, DateTime now, Guid? excluding, CancellationToken ct)
        {
            return Context.Reservations.AnyAsync(
                x =>
                    x.EquipmentItemId == itemId
                    && (excluding == null || x.Id != excluding)
                    && x.StartAtUtc < end
                    && x.EndAtUtc > start
                    && (
                        x.Status == ReservationStatus.Approved
                        || x.Status == ReservationStatus.Released
                        || (
                            x.Status == ReservationStatus.Pending
                            && x.HoldExpiresAtUtc != null
                            && x.HoldExpiresAtUtc > now
                            && x.StartAtUtc > now
                        )
                    ),
                ct
            );
        }

        // Bookings the expiry worker should close: late Pending requests and approved no-shows.
        public async Task<IReadOnlyList<ExpiryCandidate>> ExpiryCandidatesAsync(DateTime now, int size, CancellationToken ct)
        {
            return await Context
                .Reservations.AsNoTracking()
                .Where(x =>
                    x.Status == ReservationStatus.Pending
                        && (x.HoldExpiresAtUtc == null || x.HoldExpiresAtUtc <= now || x.StartAtUtc <= now)
                    // No-shows: approved bookings that ended without being released.
                    || x.Status == ReservationStatus.Approved && x.Loan == null && x.EndAtUtc <= now
                )
                .OrderBy(x => x.HoldExpiresAtUtc ?? x.EndAtUtc)
                .Select(x => new ExpiryCandidate(x.Id, x.EquipmentItemId))
                .Take(size)
                .ToListAsync(ct);
        }

        // A student's current bookings (for the "max active bookings" fair-use rule).
        public async Task<IReadOnlyList<BookingTime>> ActiveBookingTimesAsync(Guid borrowerProfileId, DateTime now, CancellationToken ct)
        {
            return await Context
                .Reservations.AsNoTracking()
                .Where(x =>
                    x.BorrowerProfileId == borrowerProfileId
                    && (
                        x.Status == ReservationStatus.Pending && x.HoldExpiresAtUtc > now && x.StartAtUtc > now
                        || x.Status == ReservationStatus.Approved && x.EndAtUtc > now
                        || x.Status == ReservationStatus.Released && x.Loan != null && x.Loan.ReturnedAtUtc == null
                    )
                )
                .Select(x => new { x.StartAtUtc, x.EndAtUtc })
                .Distinct()
                .Select(x => new BookingTime(x.StartAtUtc, x.EndAtUtc))
                .ToListAsync(ct);
        }

        public void Add(Reservation reservation) => Context.Reservations.Add(reservation);
    }
}
