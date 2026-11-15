using System.Data;
using ASI.Basecode.Data.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ASI.Basecode.Data
{
    public sealed class UnitOfWork : IUnitOfWork
    {
        private readonly AsiBasecodeDBContext _db;

        public UnitOfWork(AsiBasecodeDBContext db)
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

    internal static class PersistenceErrors
    {
        public static PersistenceException Translate(DbUpdateException error)
        {
            if (error is DbUpdateConcurrencyException)
            {
                return new(PersistenceFailure.StaleWrite);
            }

            if (error.InnerException is SqlException sql)
            {
                if (sql.Number is 2601 or 2627)
                {
                    return new(
                        PersistenceFailure.Duplicate,
                        sql.Message.Contains("StudentNumber", StringComparison.OrdinalIgnoreCase) ? "StudentNumber" : null
                    );
                }

                if (sql.Number == 547)
                {
                    return new(PersistenceFailure.Constraint);
                }
            }

            return new(PersistenceFailure.Failed);
        }
    }
}
