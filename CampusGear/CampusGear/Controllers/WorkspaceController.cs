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
        return Dashboard("Borrower", user.FullName,
            new("Active Requests", pending), new("Currently Borrowed", borrowed),
            new("Completed Reservations", completed), new("Overdue", overdue));
    }

    [Authorize(Roles = "Custodian")]
    [HttpGet("custodian/dashboard")]
    public async Task<IActionResult> Custodian()
    {
        var user = (await _users.GetUserAsync(User))!;
        var now = DateTime.UtcNow;
        return Dashboard("Custodian", user.FullName,
            new("Pending Approvals", await _db.Reservations.CountAsync(x => x.Status == ReservationStatus.Pending && x.HoldExpiresAtUtc > now && x.StartAtUtc > now)),
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
        return Dashboard("Administrator", user.FullName,
            new("Active Users", await _db.Users.CountAsync(x => x.IsActive)),
            new("Equipment Items", await _db.EquipmentItems.CountAsync(x => x.IsActive)),
            new("Pending Requests", await _db.Reservations.CountAsync(x => x.Status == ReservationStatus.Pending && x.HoldExpiresAtUtc > now && x.StartAtUtc > now)),
            new("Active Loans", await _db.Loans.CountAsync(x => x.ReturnedAtUtc == null)));
    }

    private ViewResult Dashboard(string role, string name, params DashboardMetric[] metrics)
        => View("Dashboard", new WorkspaceDashboardViewModel(role, name, metrics));
}

public sealed record DashboardMetric(string Label, int Value);
public sealed record WorkspaceDashboardViewModel(string Role, string FullName, IReadOnlyList<DashboardMetric> Metrics);
