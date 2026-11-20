using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using Microsoft.EntityFrameworkCore;

// LOAN REPOSITORY  -  loan records (created when an item is released, finished when it is returned).
namespace ASI.Basecode.Data.Repositories
{
    public sealed class LoanRepository : BaseRepository, ILoanRepository
    {
        public LoanRepository(IUnitOfWork unitOfWork, AsiBasecodeDBContext db)
            : base(unitOfWork, db) { }

        // Is the item still on loan under a DIFFERENT booking? (Blocks handing it to the next student.)
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
