using System.Data;

namespace ASI.Basecode.Data.Interfaces
{
    /// <summary>One request's shared EF context. Services choose when a workflow commits.</summary>
    public interface IUnitOfWork
    {
        Task<IDataTransaction> BeginAsync(CancellationToken cancellationToken = default);
        Task<IDataTransaction> BeginAsync(IsolationLevel isolation, CancellationToken cancellationToken = default);
        Task<int> SaveAsync(CancellationToken cancellationToken = default);
        void ClearTracking();
    }

    public interface IDataTransaction : IAsyncDisposable
    {
        Task CommitAsync(CancellationToken cancellationToken = default);
        Task RollbackAsync(CancellationToken cancellationToken = default);
    }

    public enum PersistenceFailure
    {
        StaleWrite,
        Duplicate,
        Busy,
        Constraint,
        Failed,
    }

    /// <summary>Provider-independent persistence failure; never includes connection strings or SQL text.</summary>
    public sealed class PersistenceException : Exception
    {
        private readonly PersistenceFailure _failure;
        private readonly string? _constraint;

        public PersistenceException(PersistenceFailure failure, string? constraint = null)
            : base(failure.ToString())
        {
            _failure = failure;
            _constraint = constraint;
            Failure = _failure;
            Constraint = _constraint;
        }

        public PersistenceFailure Failure { get; }

        public string? Constraint { get; }
    }
}
