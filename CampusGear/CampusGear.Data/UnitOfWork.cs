using System.Data;
using CampusGear.Data.Interfaces;
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
        catch (DbUpdateException error) { throw PersistenceErrors.Translate(error); }
    }

    public void ClearTracking() => db.ChangeTracker.Clear();

    private sealed class DataTransaction(IDbContextTransaction transaction) : IDataTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) => transaction.CommitAsync(cancellationToken);
        public Task RollbackAsync(CancellationToken cancellationToken = default) => transaction.RollbackAsync(cancellationToken);
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
