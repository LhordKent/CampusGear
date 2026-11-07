using CampusGear.Models;
namespace CampusGear.Controllers;
public sealed record AdminDashboardViewModel(string FirstName,
    DonutChart Availability, BarChart ReservationStatus, SeriesChart BorrowingTrend, SeriesChart Registrations,
    IReadOnlyList<ActivityItem> RecentActivity, IReadOnlyList<ActivityItem> PendingRegistrations, int PendingRegistrationTotal);

public sealed record CustodianDashboardViewModel(string FirstName, DeskCounts Desk,
    IReadOnlyList<ActivityItem> PendingRequests, IReadOnlyList<ActivityItem> LateLoans, SeriesChart DeskActivity,
    DonutChart Availability, BarChart Condition, int OpenMaintenanceCases, IReadOnlyList<ActivityItem> RecentLoans);
