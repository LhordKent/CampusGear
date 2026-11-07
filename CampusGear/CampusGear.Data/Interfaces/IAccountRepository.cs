namespace CampusGear.Data.Interfaces;
public interface IAccountRepository
{
    Task<bool> HasBorrowerRoleAsync(string userId, CancellationToken ct);
}
