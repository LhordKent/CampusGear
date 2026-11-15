namespace ASI.Basecode.Data.Interfaces
{
    public interface IWorkflowLockManager
    {
        Task AcquireAsync(string resource, CancellationToken cancellationToken = default);
    }
}
