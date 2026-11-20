using System.Globalization;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Resources.Messages;
using ASI.Basecode.Services.Manager;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Services
{
    public sealed class CustodianQueueService : ServiceBase, ICustodianQueueService
    {
        private readonly ICustodianReadRepository _reads;
        private readonly TimeProvider _timeProvider;

        public CustodianQueueService(ICustodianReadRepository reads, TimeProvider clock)
        {
            _reads = reads;
            _timeProvider = clock;
        }

        public async Task<CustodianDetailViewModel?> GetDetailsAsync(Guid id, bool custodian, CancellationToken cancellationToken)
        {
            var reservation = await _reads.DetailsAsync(id, cancellationToken);
            if (reservation is null)
            {
                return null;
            }

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var actions = Actions(reservation, now);

            // Administrators inspect the same record; only custodians receive action controls.
            return new CustodianDetailViewModel(
                Record: ReservationRecord.From(reservation),
                Status: ReservationDisplay.Status(reservation, now),
                CanApprove: custodian && actions.CanApprove,
                CanReject: custodian && actions.CanReject,
                CanRelease: custodian && actions.CanRelease,
                CanReturn: custodian && reservation.Status == ReservationStatus.Released && reservation.Loan is { ReturnedAtUtc: null },
                AvailabilityNote: actions.Note,
                Feedback: null,
                Error: null
            );
        }

        /// <summary>Which desk actions a reservation allows right now. Shared by the details page and the queue dialogs.</summary>
        private static (bool CanApprove, bool CanReject, bool CanRelease, string? Note) Actions(Reservation reservation, DateTime now)
        {
            var pending =
                reservation.Status == ReservationStatus.Pending && reservation.HoldExpiresAtUtc > now && reservation.StartAtUtc > now;
            var item = reservation.EquipmentItem;
            var usable =
                item.IsActive
                && !item.IsMaintenanceHold
                && item.Condition is not EquipmentCondition.Damaged;
            var releaseOpen =
                reservation.Status == ReservationStatus.Approved && usable && ReleaseWindow.IsOpen(reservation.StartAtUtc, reservation.EndAtUtc, now);
            return (pending && usable, pending, releaseOpen, GetAvailabilityNote(reservation, usable, pending, now));
        }

        private static string? GetAvailabilityNote(Reservation reservation, bool usable, bool pending, DateTime now)
        {
            // An unusable item takes precedence over its booking-time message.
            if (!usable)
            {
                return Errors.ItemUnavailable;
            }
            if (reservation.Status == ReservationStatus.Approved && now < ReleaseWindow.OpensAt(reservation.StartAtUtc))
            {
                return string.Format(Errors.ReleaseNotStarted, ManilaTime.Format(ReleaseWindow.OpensAt(reservation.StartAtUtc)));
            }
            if (reservation.Status == ReservationStatus.Approved && now < reservation.StartAtUtc)
            {
                return $"Early pickup: the booking starts at {ManilaTime.Format(reservation.StartAtUtc)}. The loan is still due at {ManilaTime.Format(reservation.EndAtUtc)}.";
            }
            if (reservation.Status == ReservationStatus.Approved && now >= reservation.EndAtUtc)
            {
                return Errors.BookingEnded;
            }
            if (reservation.Status == ReservationStatus.Pending && !pending)
            {
                return Errors.HoldExpired;
            }
            return null;
        }

        private async Task<CustodianListViewModel> BuildQueueAsync(
            CustodianQueueFilter filter,
            string queue,
            CancellationToken cancellationToken
        )
        {
            var search = filter.Search?.Trim();
            var status = filter.Status;
            var from = filter.From;
            var to = filter.To;
            var page = filter.Page;
            if (search?.Length > 100)
            {
                search = search[..100];
            }

            status = status?.ToLowerInvariant() ?? "all";
            if (queue == "approvals")
            {
                // Two tabs: Pending requests (default) and Pending release. Anything else falls back to the pending tab.
                status = status == "approved" ? "approved" : "pending";
            }
            else if (
                status is not ("pending" or "approved" or "borrowed" or "returned" or "rejected" or "cancelled" or "expired" or "late")
            )
            {
                status = "all";
            }

            DateTime? beginUtc = null,
                endUtc = null;
            string? filterError = null;
            var fromValid =
                string.IsNullOrWhiteSpace(from)
                || DateOnly.TryParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
            var toValid =
                string.IsNullOrWhiteSpace(to)
                || DateOnly.TryParseExact(to, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
            if (!fromValid || !toValid)
            {
                filterError = Errors.InvalidFilterDates;
            }
            else
            {
                DateOnly? fromDate = string.IsNullOrWhiteSpace(from)
                    ? null
                    : DateOnly.ParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                DateOnly? toDate = string.IsNullOrWhiteSpace(to)
                    ? null
                    : DateOnly.ParseExact(to, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                if (fromDate is { Year: < 2000 or > 2100 } || toDate is { Year: < 2000 or > 2100 })
                {
                    filterError = Errors.FilterDatesOutOfRange;
                }
                else if (fromDate > toDate)
                {
                    filterError = Errors.CustodianQueueInvalidDateRange;
                }
                else
                {
                    if (fromDate is { } begin)
                    {
                        beginUtc = ManilaTime.Utc(begin.ToDateTime(TimeOnly.MinValue));
                    }

                    if (toDate is { } end)
                    {
                        endUtc = ManilaTime.Utc(end.AddDays(1).ToDateTime(TimeOnly.MinValue));
                    }
                }
            }

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var result = await _reads.ListAsync(queue, status, search, beginUtc, endUtc, now, page, cancellationToken);
            var records = result.Rows;
            var total = result.Total;
            var pages = result.Pages;
            page = result.Page;
            var rows = records
                .Select(x =>
                {
                    var row = new CustodianListRow(
                        x.Id,
                        x.BorrowerProfile.User.FullName,
                        x.BorrowerProfile.User.Email ?? string.Empty,
                        x.EquipmentItem.Name,
                        x.EquipmentItem.AssetCode,
                        ManilaTime.Format(x.StartAtUtc),
                        ManilaTime.Format(x.EndAtUtc),
                        ReservationDisplay.Status(x, now),
                        x.Loan?.ConditionAtReturn?.ToString(),
                        x.Loan?.ReturnedAtUtc is { } returned ? ManilaTime.Format(returned) : null
                    );
                    if (queue != "approvals")
                    {
                        return row;
                    }

                    var actions = Actions(x, now);
                    return row with
                    {
                        Purpose = x.Purpose,
                        Notes = x.Notes,
                        EquipmentCondition = x.EquipmentItem.Condition.ToString(),
                        HoldExpiresManila = x.HoldExpiresAtUtc is { } hold ? ManilaTime.Format(hold) : null,
                        CanApprove = actions.CanApprove,
                        CanReject = actions.CanReject,
                        CanRelease = actions.CanRelease,
                        ActionNote = actions.Note,
                    };
                })
                .ToList();
            return new(
                search ?? string.Empty,
                status,
                from ?? string.Empty,
                to ?? string.Empty,
                queue,
                page,
                pages,
                total,
                rows,
                result.Pending,
                result.Approved,
                result.Active,
                null,
                filterError
            );
        }

        public Task<CustodianListViewModel> GetApprovalQueueAsync(CustodianQueueFilter filter, CancellationToken cancellationToken)
        {
            return BuildQueueAsync(filter, "approvals", cancellationToken);
        }

        public Task<CustodianListViewModel> GetReturnQueueAsync(CustodianQueueFilter filter, CancellationToken cancellationToken)
        {
            return BuildQueueAsync(filter, "active", cancellationToken);
        }

        public Task<CustodianListViewModel> GetHistoryAsync(CustodianQueueFilter filter, CancellationToken cancellationToken)
        {
            return BuildQueueAsync(filter, "history", cancellationToken);
        }
    }
}
