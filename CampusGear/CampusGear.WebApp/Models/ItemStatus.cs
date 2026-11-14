using CampusGear.Resources.Constants;

namespace CampusGear.WebApp.Models
{
    public static class ItemStatuses
    {
        public static string Label(ItemStatus status)
        {
            return status == ItemStatus.ForRepair ? "For repair" : status.ToString();
        }
    }
}
