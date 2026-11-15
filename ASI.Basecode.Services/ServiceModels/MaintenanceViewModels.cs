using System.ComponentModel.DataAnnotations;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;

namespace ASI.Basecode.Services.ServiceModels
{
    public sealed class MaintenanceEditModel
    {
        public string? RowVersion { get; set; }

        public MaintenanceStatus Status { get; set; }

        public EquipmentCondition Condition { get; set; } = EquipmentCondition.Good;

        [StringLength(2000)]
        public string? Resolution { get; set; }
    }

    public sealed record MaintenancePage(MaintenanceListData Data, string Status, string Query);
}
