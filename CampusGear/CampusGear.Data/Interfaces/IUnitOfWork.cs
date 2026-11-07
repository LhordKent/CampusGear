using System.Data;

namespace CampusGear.Data.Interfaces;

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

public enum PersistenceFailure { StaleWrite, Duplicate, Busy }

/// <summary>Provider-independent persistence failure; never includes connection strings or SQL text.</summary>
public sealed class PersistenceException(PersistenceFailure failure, string? constraint = null) : Exception(failure.ToString())
{
    public PersistenceFailure Failure { get; } = failure;
    public string? Constraint { get; } = constraint;
}
