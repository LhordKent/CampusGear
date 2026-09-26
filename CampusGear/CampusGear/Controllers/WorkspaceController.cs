using CampusGear.Data;
using CampusGear.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CampusGear.Controllers;

public sealed class WorkspaceController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public WorkspaceController(ApplicationDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    [Authorize(Roles = "Borrower")]
    [HttpGet("borrower/dashboard")]
    public async Task<IActionResult> Borrower()
    {
        var user = (await _users.GetUserAsync(User))!;
        var profile = await _db.BorrowerProfiles.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == user.Id);
        if (profile is null) return Forbid();

        var now = DateTime.UtcNow;
        var reservationQuery = _db.Reservations.AsNoTracking()
            .Where(x => x.BorrowerProfileId == profile.Id);
        var pending = await reservationQuery.CountAsync(x => x.Status == ReservationStatus.Pending && x.HoldExpiresAtUtc > now && x.StartAtUtc > now);
        var borrowed = await _db.Loans.AsNoTracking().CountAsync(x =>
            x.Reservation.BorrowerProfileId == profile.Id && x.ReturnedAtUtc == null);
        var completed = await reservationQuery.CountAsync(x => x.Status == ReservationStatus.Completed);
        var overdue = await _db.Loans.AsNoTracking().CountAsync(x =>
            x.Reservation.BorrowerProfileId == profile.Id && x.ReturnedAtUtc == null && x.DueAtUtc < now);
        var activity = await reservationQuery.Include(x => x.EquipmentItem).Include(x => x.BorrowerProfile.User)
            .Where(x => (x.Status == ReservationStatus.Pending && x.HoldExpiresAtUtc > now && x.StartAtUtc > now) ||
                x.Status == ReservationStatus.Approved || x.Status == ReservationStatus.Released)
            .OrderBy(x => x.StartAtUtc).Take(3).ToListAsync();
        return Dashboard("Borrower", user.FullName, activity,
            new("Active Requests", pending), new("Currently Borrowed", borrowed),
            new("Completed Reservations", completed), new("Overdue", overdue));
    }

    [Authorize(Roles = "Custodian")]
    [HttpGet("custodian/dashboard")]
    public async Task<IActionResult> Custodian()
    {
        var user = (await _users.GetUserAsync(User))!;
        var now = DateTime.UtcNow;
        var activity = await _db.Reservations.AsNoTracking().Include(x => x.EquipmentItem).Include(x => x.BorrowerProfile.User)
            .Where(x => (x.Status == ReservationStatus.Pending && x.HoldExpiresAtUtc > now && x.StartAtUtc > now) || x.Status == ReservationStatus.Approved)
            .OrderBy(x => x.StartAtUtc).Take(3).ToListAsync();
        return Dashboard("Custodian", user.FullName, activity,
            new("Pending Requests", await _db.Reservations.CountAsync(x => x.Status == ReservationStatus.Pending && x.HoldExpiresAtUtc > now && x.StartAtUtc > now)),
            new("Currently Borrowed", await _db.Loans.CountAsync(x => x.ReturnedAtUtc == null)),
            new("Under Maintenance", await _db.EquipmentItems.CountAsync(x => x.IsMaintenanceHold)),
            new("Overdue", await _db.Loans.CountAsync(x => x.ReturnedAtUtc == null && x.DueAtUtc < now)));
    }

    [Authorize(Roles = "Administrator")]
    [HttpGet("admin/dashboard")]
    public async Task<IActionResult> Administrator()
    {
        var user = (await _users.GetUserAsync(User))!;
        var now = DateTime.UtcNow;
        var activity = await _db.Reservations.AsNoTracking().Include(x => x.EquipmentItem).Include(x => x.BorrowerProfile.User)
            .OrderByDescending(x => x.UpdatedAtUtc).Take(3).ToListAsync();
        return Dashboard("Administrator", user.FullName, activity,
            new("Total Equipment", await _db.EquipmentItems.CountAsync(x => x.IsActive)),
            new("Active Borrowings", await _db.Loans.CountAsync(x => x.ReturnedAtUtc == null)),
            new("Pending Approvals", await _db.Reservations.CountAsync(x => x.Status == ReservationStatus.Pending && x.HoldExpiresAtUtc > now && x.StartAtUtc > now)),
            new("Total Borrowers", await _db.UserRoles.CountAsync(x => _db.Roles.Any(r => r.Id == x.RoleId && r.Name == "Borrower"))));
    }

    private ViewResult Dashboard(string role, string name, IReadOnlyList<Reservation> activity, params DashboardMetric[] metrics)
        => View("Dashboard", new WorkspaceDashboardViewModel(role, name, metrics, activity));
}

public sealed record DashboardMetric(string Label, int Value);
public sealed record WorkspaceDashboardViewModel(string Role, string FullName, IReadOnlyList<DashboardMetric> Metrics,
    IReadOnlyList<Reservation> Activity);
