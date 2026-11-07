namespace CampusGear.Data.Interfaces;

public interface IWorkflowLockManager
{
    Task AcquireAsync(string resource, CancellationToken cancellationToken = default);
}
