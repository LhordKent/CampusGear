using CampusGear.Data;
using Microsoft.EntityFrameworkCore;

namespace CampusGear.Models;

/// <summary>
/// The status of one equipment unit, derived from its records rather than stored, so it can never disagree with them.
/// Flow: Available → Reserved (an approved booking whose time has started) → Borrowed (released, not yet returned)
/// → Available, or For repair when returned Damaged/Unserviceable. The reservation service blocks every other move:
/// only approved bookings can be released, only open loans can be returned, and units on hold cannot be released.
/// </summary>
public enum ItemStatus { Available, Reserved, Borrowed, ForRepair, Inactive }

public static class ItemStatuses
{
    public static string Label(ItemStatus status) => status switch
    {
        ItemStatus.ForRepair => "For repair",
        _ => status.ToString()
    };

    /// <summary>Current status of every unit, keyed by item id. Borrowed outranks For repair: a unit out on loan is out.</summary>
    public static async Task<Dictionary<Guid, ItemStatus>> ForAllAsync(ApplicationDbContext db, DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var items = await db.EquipmentItems.AsNoTracking()
            .Select(x => new
            {
                x.Id, x.IsActive,
                Unusable = x.IsMaintenanceHold || x.Condition == EquipmentCondition.Damaged ||
                    x.Condition == EquipmentCondition.Unserviceable
            })
            .ToListAsync(cancellationToken);
        var borrowed = (await db.Loans.AsNoTracking().Where(x => x.ReturnedAtUtc == null)
            .Select(x => x.Reservation.EquipmentItemId).ToListAsync(cancellationToken)).ToHashSet();
        var reserved = (await db.Reservations.AsNoTracking()
            .Where(x => x.Status == ReservationStatus.Approved && x.StartAtUtc <= nowUtc && x.EndAtUtc > nowUtc)
            .Select(x => x.EquipmentItemId).ToListAsync(cancellationToken)).ToHashSet();
        return items.ToDictionary(x => x.Id, x =>
            !x.IsActive ? ItemStatus.Inactive
            : borrowed.Contains(x.Id) ? ItemStatus.Borrowed
            : x.Unusable ? ItemStatus.ForRepair
            : reserved.Contains(x.Id) ? ItemStatus.Reserved
            : ItemStatus.Available);
    }
}
