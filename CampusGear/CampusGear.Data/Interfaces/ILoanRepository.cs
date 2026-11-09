using CampusGear.Models;
namespace CampusGear.Data.Interfaces;
public interface ILoanRepository
{
    Task<bool> HasOutstandingExceptAsync(Guid itemId, Guid reservationId, CancellationToken ct);
    void Add(Loan loan);
}
