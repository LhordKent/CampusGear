using System.Data;
using CampusGear.Data.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CampusGear.Data;

/// <summary>Locks belong to the current transaction, shared by all repositories and Identity stores.</summary>
public sealed class WorkflowLockManager(ApplicationDbContext db) : IWorkflowLockManager
{
    public async Task AcquireAsync(string resource, CancellationToken cancellationToken = default)
    {
        var result = new SqlParameter("@lockResult", SqlDbType.Int) { Direction = ParameterDirection.Output };
        var key = new SqlParameter("@resource", SqlDbType.NVarChar, 255) { Value = resource };
        await db.Database.ExecuteSqlRawAsync(
            "EXEC @lockResult = sp_getapplock @Resource=@resource, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000",
            new object[] { result, key }, cancellationToken);
        if (result.Value is not int code || code < 0) throw new PersistenceException(PersistenceFailure.Busy);
    }
}
