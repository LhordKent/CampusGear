using ASI.Basecode.Resources.Constants;

namespace ASI.Basecode.WebApp.Models
{
    /// <summary>Input for Views/Custodian/_ConditionOptions: the equipment conditions offered as option buttons.</summary>
    public sealed record ConditionOptionsModel(
        string Legend,
        IReadOnlyList<EquipmentCondition> Options,
        EquipmentCondition Selected,
        string? Help = null,
        bool Disabled = false
    )
    {
        public static readonly EquipmentCondition[] ReleaseConditions =
            [EquipmentCondition.Excellent, EquipmentCondition.Good, EquipmentCondition.Fair];

        public static readonly EquipmentCondition[] ReturnConditions = Enum.GetValues<EquipmentCondition>();
    }
}
