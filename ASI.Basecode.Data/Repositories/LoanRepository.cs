using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace ASI.Basecode.Data.Repositories
{
    public sealed class LoanRepository : BaseRepository, ILoanRepository
    {
        public LoanRepository(IUnitOfWork unitOfWork, AsiBasecodeDBContext db)
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
