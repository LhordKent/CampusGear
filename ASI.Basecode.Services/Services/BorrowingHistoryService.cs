using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Resources.Messages;
using ASI.Basecode.Services.Manager;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Services
{
    public sealed class BorrowingHistoryService : ServiceBase, IBorrowingHistoryService
    {
        private readonly IBorrowerReadRepository _reads;
        private readonly TimeProvider _timeProvider;

        public BorrowingHistoryService(IBorrowerReadRepository reads, TimeProvider clock)
        {
            _reads = reads;
            _timeProvider = clock;
        }

        public async Task<OperationResult<HistoryPageViewModel>> HistoryAsync(
            RequestContext actor,
            string? status,
            string? from,
            string? to,
            string? q,
            int page = 1,
            CancellationToken cancellationToken = default
        )
        {
            var profile = await OwnProfileAsync(actor, cancellationToken);
            if (profile is null)
            {
                return new(OperationOutcome.Forbidden);
            }

            status = NormalizeStatus(status);
            q = q?.Trim();
            if (q?.Length > 100)
            {
                q = q[..100];
            }

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var errors = new List<FieldError>();
            DateTime? fromUtcValue = null,
                toUtcValue = null;
            if (ManilaTime.TryParseDate(from, out var fromDate))
            {
                var fromUtc = ManilaTime.Utc(fromDate.ToDateTime(TimeOnly.MinValue));
                fromUtcValue = fromUtc;
            }
            else if (!string.IsNullOrEmpty(from))
            {
                errors.Add(new("from", Errors.InvalidFromDate));
            }

            if (ManilaTime.TryParseDate(to, out var toDate) && toDate < DateOnly.MaxValue)
            {
                var toUtc = ManilaTime.Utc(toDate.AddDays(1).ToDateTime(TimeOnly.MinValue));
                toUtcValue = toUtc;
            }
            else if (!string.IsNullOrEmpty(to))
            {
                errors.Add(new("to", Errors.InvalidToDate));
            }

            if (ManilaTime.TryParseDate(from, out fromDate) && ManilaTime.TryParseDate(to, out toDate) && fromDate > toDate)
            {
                errors.Add(new("to", Errors.BorrowingHistoryInvalidDateRange));
            }

            var result = await _reads.HistoryAsync(profile.Id, status, q, fromUtcValue, toUtcValue, now, page, cancellationToken);
            var records = result.Rows;
            var count = result.Total;
            var pageCount = result.Pages;
            page = result.Page;
            var rows = records
                .Select(x => new HistoryRow(
                    x.Id,
                    x.EquipmentItem.Name,
                    x.EquipmentItem.AssetCode,
                    ManilaTime.Format(x.StartAtUtc),
                    ManilaTime.Format(x.EndAtUtc),
                    ReservationDisplay.Status(x, now),
                    x.Purpose,
                    (x.Status is ReservationStatus.Pending or ReservationStatus.Approved)
                        && (x.Status != ReservationStatus.Pending || x.HoldExpiresAtUtc > now),
                    x.Loan?.ReturnedAtUtc is { } returned ? ManilaTime.Format(returned) : null,
                    x.Loan?.ConditionAtReturn?.ToString()
                ))
                .ToList();
            return OperationResult<HistoryPageViewModel>.Ok(
                new HistoryPageViewModel(
                    rows,
                    status,
                    from ?? string.Empty,
                    to ?? string.Empty,
                    q ?? string.Empty,
                    page,
                    pageCount,
                    count,
                    null
                )
            ) with
            {
                Errors = errors,
            };
        }

        private Task<BorrowerProfile?> OwnProfileAsync(RequestContext actor, CancellationToken ct)
        {
            return _reads.FindProfileAsync(actor.UserId, ct);
        }

        private static string NormalizeStatus(string? status)
        {
            return status?.ToLowerInvariant() switch
            {
                "pending" or "approved" or "borrowed" or "returned" or "late" or "cancelled" => status.ToLowerInvariant(),
                _ => "all",
            };
        }
    }
}
