using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;

namespace ASI.Basecode.Data.Interfaces
{
    public interface ILoanRepository
    {
        Task<bool> HasOutstandingExceptAsync(Guid itemId, Guid reservationId, CancellationToken ct);
        void Add(Loan loan);
    }
}
