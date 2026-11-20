using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ASI.Basecode.Data.Migrations
{
    /// <summary>
    /// "Unserviceable" was removed from EquipmentCondition; Damaged covers it (maintenance hold, not bookable).
    /// Conditions are stored as text, so any row still holding the old name would no longer load. Data only:
    /// the schema is unchanged, which is why this migration has no model snapshot of its own.
    /// </summary>
    [DbContext(typeof(AsiBasecodeDBContext))]
    [Migration("20261121010000_RetireUnserviceableCondition")]
    public partial class RetireUnserviceableCondition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE [EquipmentItems] SET [Condition] = N'Damaged' WHERE [Condition] = N'Unserviceable';");
            migrationBuilder.Sql("UPDATE [Loans] SET [ConditionAtRelease] = N'Damaged' WHERE [ConditionAtRelease] = N'Unserviceable';");
            migrationBuilder.Sql("UPDATE [Loans] SET [ConditionAtReturn] = N'Damaged' WHERE [ConditionAtReturn] = N'Unserviceable';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Not reversible: the converted rows can no longer be told apart from ones that were Damaged to begin with.
        }
    }
}
