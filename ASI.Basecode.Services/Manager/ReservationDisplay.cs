using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;

namespace ASI.Basecode.Services.Manager
{
    public static class ReservationDisplay
    {
        /// <summary>A loan may be returned at any time after release; one returned before its due time is an early return.</summary>
        public static bool IsReturnedEarly(Loan? loan)
        {
            return loan is { ReturnedAtUtc: { } returned } && returned < loan.DueAtUtc;
        }

        /// <summary>
        /// When the booking actually stops holding the item: the return time for an early return, otherwise the booked end.
        /// Calendars use this so returned equipment shows as free for the rest of the original booking.
        /// </summary>
        public static DateTime BookedUntil(Reservation reservation)
        {
            return reservation.Loan is { ReturnedAtUtc: { } returned } && returned < reservation.EndAtUtc ? returned : reservation.EndAtUtc;
        }

        public static string Status(Reservation reservation, DateTime now)
        {
            if (reservation.Loan is { ReturnedAtUtc: null } loan && ReturnGrace.IsLate(loan.DueAtUtc, now))
            {
                return "Overdue";
            }

            if (reservation.Loan is { ReturnedAtUtc: { } returned } completed && ReturnGrace.IsLate(completed.DueAtUtc, returned))
            {
                return "Returned late";
            }

            if (IsReturnedEarly(reservation.Loan))
            {
                return "Returned early";
            }

            if (
                reservation.Status == ReservationStatus.Pending
                && (reservation.HoldExpiresAtUtc == null || reservation.HoldExpiresAtUtc <= now || reservation.StartAtUtc <= now)
            )
            {
                return "Expired";
            }

            return reservation.Status switch
            {
                ReservationStatus.Released => "Borrowed",
                ReservationStatus.Completed => "Returned",
                _ => reservation.Status.ToString(),
            };
        }
    }
}
