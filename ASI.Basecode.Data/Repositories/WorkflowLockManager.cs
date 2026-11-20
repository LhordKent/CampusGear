using System.Data;
using ASI.Basecode.Data.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

// =====================================================================
// WORKFLOW LOCK MANAGER  -  named locks inside SQL Server (sp_getapplock).
// Example: "CampusGear:EquipmentItem:{id}" - while one request holds it, any other request
// that asks for the same name WAITS (up to 10 seconds, then "Busy").
// The lock is released automatically when the transaction commits or rolls back.
// This is what stops two custodians approving / two students booking the same item at the same moment.
// =====================================================================
namespace ASI.Basecode.Data.Repositories
{
    /// <summary>Locks belong to the current transaction, shared by all repositories and Identity stores.</summary>
    public sealed class WorkflowLockManager : BaseRepository, IWorkflowLockManager
    {
        public WorkflowLockManager(IUnitOfWork unitOfWork, AsiBasecodeDBContext db)
            : base(unitOfWork, db) { }

        public async Task AcquireAsync(string resource, CancellationToken cancellationToken = default)
        {
            var result = new SqlParameter("@lockResult", SqlDbType.Int) { Direction = ParameterDirection.Output };
            var key = new SqlParameter("@resource", SqlDbType.NVarChar, 255) { Value = resource };
            await Context.Database.ExecuteSqlRawAsync(
                "EXEC @lockResult = sp_getapplock @Resource=@resource, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000",
                new object[] { result, key },
                cancellationToken
            );
            if (result.Value is not int code || code < 0)
            {
                throw new PersistenceException(PersistenceFailure.Busy);
            }
        }
    }
}
