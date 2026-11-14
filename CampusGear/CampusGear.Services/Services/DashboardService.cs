using System.Globalization;
using CampusGear.Controllers;
using CampusGear.Data.Interfaces;
using CampusGear.Models;
using Microsoft.AspNetCore.Identity;
namespace CampusGear.Services;
public sealed class DashboardService(IDashboardReadRepository reads, UserManager<ApplicationUser> users,
    IEquipmentAvailabilityService availability, TimeProvider clock) : IDashboardService
{
    private const int TrendWeeks = 12;
    private const int TrendDays = 14;
    private const int RegistrationMonths = 6;

    public async Task<CustodianDashboardViewModel> GetCustodianAsync(string userId, CancellationToken cancellationToken)
    {
        var user = (await users.FindByIdAsync(userId))!;
        var now = clock.GetUtcNow().UtcDateTime;
        var todayEndUtc = CustodianTime.Utc(CustodianTime.Local(now).Date.AddDays(1));

        var desk = await reads.DeskAsync(now, todayEndUtc, cancellationToken);
        var pendingRequests = await reads.PendingRequestsAsync(now, cancellationToken);
        var lateLoans = await reads.DueLoansAsync(todayEndUtc, cancellationToken);

        var model = new CustodianDashboardViewModel(user.FirstName, new DeskCounts(desk.Pending, desk.ReadyToRelease, desk.Overdue, desk.DueToday),
            pendingRequests.Select(x => new ActivityItem(x.BorrowerProfile.User.FullName,
                $"{x.EquipmentItem.Name} · {x.EquipmentItem.AssetCode}", "Pending",
                $"Needed {CustodianTime.Format(x.StartAtUtc)}", $"/custodian/reservations/{x.Id}")).ToList(),
            lateLoans.Select(x => new ActivityItem(x.Reservation.BorrowerProfile.User.FullName,
                $"{x.Reservation.EquipmentItem.Name} · {x.Reservation.EquipmentItem.AssetCode}",
                x.DueAtUtc < now ? "Overdue" : "Due today", $"Due {CustodianTime.Format(x.DueAtUtc)}",
                $"/custodian/reservations/{x.ReservationId}")).ToList(),
            await DeskActivityAsync(now, cancellationToken),
            await AvailabilityAsync(false, cancellationToken),
            await ConditionAsync(cancellationToken),
            await reads.OpenCasesAsync(cancellationToken),
            await RecentLoanEventsAsync(now, cancellationToken));
        return model;
    }

    public async Task<AdminDashboardViewModel> GetAdministratorAsync(string userId, CancellationToken cancellationToken)
    {
        var user = (await users.FindByIdAsync(userId))!;
        var now = clock.GetUtcNow().UtcDateTime;

        var pendingRegistrations = await reads.PendingRegistrationsAsync(cancellationToken);

        var recent = await reads.RecentReservationsAsync(cancellationToken);
        var registrations = await reads.RegistrationsPendingAsync(cancellationToken);

        var model = new AdminDashboardViewModel(user.FirstName,
            await AvailabilityAsync(true, cancellationToken),
            await ReservationStatusAsync(now, cancellationToken),
            await BorrowingTrendAsync(now, cancellationToken),
            await RegistrationsAsync(now, cancellationToken),
            recent.Select(x => new ActivityItem(x.BorrowerProfile.User.FullName,
                $"{x.EquipmentItem.Name} · {x.EquipmentItem.AssetCode}", CustodianTime.Status(x, now),
                $"Updated {CustodianTime.Format(x.UpdatedAtUtc)}", $"/custodian/reservations/{x.Id}")).ToList(),
            registrations.Select(x => new ActivityItem(x.FullName,
                string.Join(" · ", new[] { x.BorrowerProfile?.StudentNumber, x.BorrowerProfile?.Department, x.Email }
                    .Where(value => !string.IsNullOrWhiteSpace(value))),
                "Pending approval", $"Submitted {CustodianTime.Format(x.CreatedAtUtc)}", $"/admin/users/{x.Id}/edit")).ToList(),
            pendingRegistrations);
        return model;
    }

    /// <summary>Active units by what they can do right now. Each unit lands in exactly one state.</summary>
    private async Task<DonutChart> AvailabilityAsync(bool admin, CancellationToken cancellationToken)
    {
        var statuses = (await availability.GetStatesAsync(clock.GetUtcNow().UtcDateTime, cancellationToken)).Values
            .Where(x => x != ItemStatus.Inactive).ToList();
        int Count(ItemStatus status) => statuses.Count(x => x == status);
        // Colour slots run 1-4 in the order of the status flow (an order validated for colour-blind separation),
        // so each status keeps its colour on every dashboard.
        return new DonutChart("Equipment status", "Every active unit, by its status right now", "active units",
        [
            new("Available", Count(ItemStatus.Available), 1, admin ? "/admin/equipment" : "/custodian/calendar"),
            new("Reserved", Count(ItemStatus.Reserved), 2, admin ? "/admin/history?status=approved" : "/custodian/approvals?status=approved"),
            new("Borrowed", Count(ItemStatus.Borrowed), 3, admin ? "/admin/history?status=borrowed" : "/custodian/returns"),
            new("For repair", Count(ItemStatus.ForRepair), 4, "/admin/maintenance")
        ]);
    }

    private async Task<BarChart> ConditionAsync(CancellationToken cancellationToken)
    {
        var counts = await reads.ConditionsAsync(cancellationToken);
        return new BarChart("Equipment condition", "Active units by their last recorded condition",
            Enum.GetValues<EquipmentCondition>()
                .Select(condition => new ChartSlice(condition.ToString(),
                    counts.FirstOrDefault(x => x.Key == condition)?.Count ?? 0)).ToList());
    }

    /// <summary>All reservations by lifecycle status. A pending hold that lapsed counts as expired, as the queues show it.</summary>
    private async Task<BarChart> ReservationStatusAsync(DateTime now, CancellationToken cancellationToken)
    {
        var counts = await reads.StatusesAsync(cancellationToken);
        var livePending = await reads.LivePendingAsync(now, cancellationToken);
        int Count(ReservationStatus status) => counts.FirstOrDefault(x => x.Key == status)?.Count ?? 0;
        return new BarChart("Reservation requests", "All requests by current status",
        [
            new("Pending", livePending, 1, "/admin/history?status=pending"),
            new("Approved", Count(ReservationStatus.Approved), 1, "/admin/history?status=approved"),
            new("Borrowed", Count(ReservationStatus.Released), 1, "/admin/history?status=borrowed"),
            new("Returned", Count(ReservationStatus.Completed), 1, "/admin/history?status=returned"),
            new("Rejected", Count(ReservationStatus.Rejected), 1, "/admin/history?status=rejected"),
            new("Cancelled", Count(ReservationStatus.Cancelled), 1, "/admin/history?status=cancelled"),
            new("Expired", Count(ReservationStatus.Expired) + Count(ReservationStatus.Pending) - livePending, 1,
                "/admin/history?status=expired")
        ]);
    }

    /// <summary>Requests submitted and equipment released per Manila week, Monday to Sunday.</summary>
    private async Task<SeriesChart> BorrowingTrendAsync(DateTime now, CancellationToken cancellationToken)
    {
        var today = CustodianTime.Local(now).Date;
        var firstWeek = today.AddDays(-(((int)today.DayOfWeek + 6) % 7)).AddDays(-7 * (TrendWeeks - 1));
        var fromUtc = CustodianTime.Utc(firstWeek);
        var requested = await reads.RequestTimesAsync(fromUtc, cancellationToken);
        var released = await reads.ReleaseTimesAsync(fromUtc, cancellationToken);
        int[] Bucket(IEnumerable<DateTime> times)
        {
            var values = new int[TrendWeeks];
            foreach (var time in times)
            {
                var index = (int)((CustodianTime.Local(time).Date - firstWeek).TotalDays / 7);
                if (index is >= 0 and < TrendWeeks) values[index]++;
            }
            return values;
        }
        return new SeriesChart("Borrowing activity", $"Weekly totals for the last {TrendWeeks} weeks", "line",
            Enumerable.Range(0, TrendWeeks).Select(x => firstWeek.AddDays(7 * x)
                .ToString("MMM d", CultureInfo.InvariantCulture)).ToList(),
            [new("Requests submitted", 1, Bucket(requested)), new("Equipment released", 2, Bucket(released))]);
    }

    /// <summary>Releases and returns per Manila day, for the custodian's desk.</summary>
    private async Task<SeriesChart> DeskActivityAsync(DateTime now, CancellationToken cancellationToken)
    {
        var firstDay = CustodianTime.Local(now).Date.AddDays(-(TrendDays - 1));
        var fromUtc = CustodianTime.Utc(firstDay);
        var released = await reads.ReleaseTimesAsync(fromUtc, cancellationToken);
        var returned = await reads.ReturnTimesAsync(fromUtc, cancellationToken);
        int[] Bucket(IEnumerable<DateTime> times)
        {
            var values = new int[TrendDays];
            foreach (var time in times)
            {
                var index = (int)(CustodianTime.Local(time).Date - firstDay).TotalDays;
                if (index is >= 0 and < TrendDays) values[index]++;
            }
            return values;
        }
        return new SeriesChart("Releases and returns", $"Daily desk activity for the last {TrendDays} days", "line",
            Enumerable.Range(0, TrendDays).Select(x => firstDay.AddDays(x)
                .ToString("MMM d", CultureInfo.InvariantCulture)).ToList(),
            [new("Released", 1, Bucket(released)), new("Returned", 2, Bucket(returned))]);
    }

    /// <summary>New borrower registrations per Manila month.</summary>
    private async Task<SeriesChart> RegistrationsAsync(DateTime now, CancellationToken cancellationToken)
    {
        var local = CustodianTime.Local(now);
        var firstMonth = new DateTime(local.Year, local.Month, 1).AddMonths(-(RegistrationMonths - 1));
        var fromUtc = CustodianTime.Utc(firstMonth);
        var created = await reads.RegistrationTimesAsync(fromUtc, cancellationToken);
        var values = new int[RegistrationMonths];
        foreach (var time in created.Select(CustodianTime.Local))
        {
            var index = (time.Year - firstMonth.Year) * 12 + time.Month - firstMonth.Month;
            if (index is >= 0 and < RegistrationMonths) values[index]++;
        }
        return new SeriesChart("Borrower registrations", $"New sign-ups per month, last {RegistrationMonths} months", "column",
            Enumerable.Range(0, RegistrationMonths).Select(x => firstMonth.AddMonths(x)
                .ToString("MMM", CultureInfo.InvariantCulture)).ToList(),
            [new("Registrations", 1, values)]);
    }

    /// <summary>The latest releases and returns, newest first.</summary>
    private async Task<IReadOnlyList<ActivityItem>> RecentLoanEventsAsync(DateTime now, CancellationToken cancellationToken)
    {
        var loans = await reads.RecentLoansAsync(cancellationToken);
        return loans.Select(x => new ActivityItem(x.Reservation.BorrowerProfile.User.FullName,
                $"{x.Reservation.EquipmentItem.Name} · {x.Reservation.EquipmentItem.AssetCode}",
                x.ReturnedAtUtc is null ? x.DueAtUtc < now ? "Overdue" : "Released" : x.ReturnedAtUtc > x.DueAtUtc ? "Returned late" : CustodianTime.IsReturnedEarly(x) ? "Returned early" : "Returned",
                x.ReturnedAtUtc is { } returned ? $"Returned {CustodianTime.Format(returned)}" : $"Released {CustodianTime.Format(x.ReleasedAtUtc)}",
                $"/custodian/reservations/{x.ReservationId}"))
            .ToList();
    }
}
