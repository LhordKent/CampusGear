namespace CampusGear.Models;
public static class ItemStatuses
{
    public static string Label(ItemStatus status) => status == ItemStatus.ForRepair ? "For repair" : status.ToString();
}
