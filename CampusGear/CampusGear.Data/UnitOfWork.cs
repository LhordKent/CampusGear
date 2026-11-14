using System.Data;
using CampusGear.Data.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CampusGear.Data
{
    public sealed class UnitOfWork : IUnitOfWork
    {
        private readonly ApplicationDbContext _db;

        public UnitOfWork(ApplicationDbContext db)
        {
            _db = db;
        }

        public Task<IDataTransaction> BeginAsync(CancellationToken cancellationToken = default)
        {
            return BeginAsync(IsolationLevel.ReadCommitted, cancellationToken);
        }

        public async Task<IDataTransaction> BeginAsync(IsolationLevel isolation, CancellationToken cancellationToken = default)
        {
            return new DataTransaction(await _db.Database.BeginTransactionAsync(isolation, cancellationToken));
        }

        public async Task<int> SaveAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException error)
            {
                throw PersistenceErrors.Translate(error);
            }
        }

        public void ClearTracking() => _db.ChangeTracker.Clear();

        private sealed class DataTransaction : IDataTransaction
        {
            private readonly IDbContextTransaction _transaction;

            public DataTransaction(IDbContextTransaction transaction)
            {
                _transaction = transaction;
            }

            public Task CommitAsync(CancellationToken cancellationToken = default) => _transaction.CommitAsync(cancellationToken);

            public Task RollbackAsync(CancellationToken cancellationToken = default) => _transaction.RollbackAsync(cancellationToken);

            public ValueTask DisposeAsync() => _transaction.DisposeAsync();
        }
    }
}
