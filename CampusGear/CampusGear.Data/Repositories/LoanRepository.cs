using CampusGear.Data.Interfaces;
using CampusGear.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace CampusGear.Data.Repositories
{
    public sealed class LoanRepository : BaseRepository, ILoanRepository
    {
        public LoanRepository(IUnitOfWork unitOfWork, ApplicationDbContext db)
            : base(unitOfWork, db) { }

        public Task<bool> HasOutstandingExceptAsync(Guid itemId, Guid reservationId, CancellationToken ct)
        {
            return Context.Loans.AnyAsync(
                x => x.Reservation.EquipmentItemId == itemId && x.ReservationId != reservationId && x.ReturnedAtUtc == null,
                ct
            );
        }

        public void Add(Loan loan) => Context.Loans.Add(loan);
    }
}
