using System.Data;
using CampusGear.Data.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CampusGear.Data;

public sealed class UnitOfWork(ApplicationDbContext db) : IUnitOfWork
{
    public Task<IDataTransaction> BeginAsync(CancellationToken cancellationToken = default) =>
        BeginAsync(IsolationLevel.ReadCommitted, cancellationToken);

    public async Task<IDataTransaction> BeginAsync(IsolationLevel isolation, CancellationToken cancellationToken = default) =>
        new DataTransaction(await db.Database.BeginTransactionAsync(isolation, cancellationToken));

    public async Task<int> SaveAsync(CancellationToken cancellationToken = default)
    {
        try { return await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new PersistenceException(PersistenceFailure.StaleWrite); }
        catch (DbUpdateException e) when (e.InnerException is SqlException { Number: 2601 or 2627 } sql)
        {
            throw new PersistenceException(PersistenceFailure.Duplicate,
                sql.Message.Contains("StudentNumber", StringComparison.OrdinalIgnoreCase) ? "StudentNumber" : null);
        }
    }

    public void ClearTracking() => db.ChangeTracker.Clear();

    private sealed class DataTransaction(IDbContextTransaction transaction) : IDataTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) => transaction.CommitAsync(cancellationToken);
        public Task RollbackAsync(CancellationToken cancellationToken = default) => transaction.RollbackAsync(cancellationToken);
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
