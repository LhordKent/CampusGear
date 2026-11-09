using CampusGear.Data.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
namespace CampusGear.Data;

internal static class PersistenceErrors
{
    public static PersistenceException Translate(DbUpdateException error)
    {
        if (error is DbUpdateConcurrencyException) return new(PersistenceFailure.StaleWrite);
        if (error.InnerException is SqlException sql)
        {
            if (sql.Number is 2601 or 2627)
                return new(PersistenceFailure.Duplicate,
                    sql.Message.Contains("StudentNumber", StringComparison.OrdinalIgnoreCase) ? "StudentNumber" : null);
            if (sql.Number == 547) return new(PersistenceFailure.Constraint);
        }
        return new(PersistenceFailure.Failed);
    }
}
