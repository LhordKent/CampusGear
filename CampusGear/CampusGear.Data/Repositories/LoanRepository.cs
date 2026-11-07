using CampusGear.Data.Interfaces;
using CampusGear.Models;
using Microsoft.EntityFrameworkCore;
namespace CampusGear.Data.Repositories;
public sealed class LoanRepository(ApplicationDbContext db) : ILoanRepository
{
    public Task<bool> HasOutstandingExceptAsync(Guid itemId, Guid reservationId, CancellationToken ct) =>
        db.Loans.AnyAsync(x => x.Reservation.EquipmentItemId == itemId && x.ReservationId != reservationId && x.ReturnedAtUtc == null, ct);
    public void Add(Loan loan) => db.Loans.Add(loan);
}
