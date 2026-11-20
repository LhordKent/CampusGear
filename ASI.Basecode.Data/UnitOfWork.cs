using System.Data;
using ASI.Basecode.Data.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

// =====================================================================
// UNIT OF WORK  -  one database "session" per web request, shared by every repository.
//   BeginAsync()  -> start a transaction (needed before taking a lock)
//   SaveAsync()   -> write all changes made by the repositories at once
//   Commit        -> make them permanent;  no commit = everything is rolled back
// Database errors are translated into simple PersistenceFailure values (StaleWrite, Duplicate, Constraint, Failed).
// ("Busy" comes from WorkflowLockManager when a lock is not free within 10 seconds.)
// =====================================================================
namespace ASI.Basecode.Data
{
    public sealed class UnitOfWork : IUnitOfWork
    {
        private readonly AsiBasecodeDBContext _db;

        public UnitOfWork(AsiBasecodeDBContext db)
        {
            _db = db;
        }

        // Starts a transaction (default isolation).
        public Task<IDataTransaction> BeginAsync(CancellationToken cancellationToken = default)
        {
            return BeginAsync(IsolationLevel.ReadCommitted, cancellationToken);
        }

        public async Task<IDataTransaction> BeginAsync(IsolationLevel isolation, CancellationToken cancellationToken = default)
        {
            return new DataTransaction(await _db.Database.BeginTransactionAsync(isolation, cancellationToken));
        }

        // Saves every pending change. Duplicate key or "someone else changed this row" become PersistenceException.
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

        // Forgets everything loaded so far (used after a failed save, so stale objects aren't reused).
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
        // Turns a raw database error into StaleWrite / Duplicate / Constraint / Failed.
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
