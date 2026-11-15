using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Resources.Views;

namespace ASI.Basecode.WebApp.Models
{
    public sealed record WorkspaceNavigationItem(string Path, string Label, string Icon, string? Group = null);

    public static class WorkspaceNavigation
    {
        public static IReadOnlyList<WorkspaceNavigationItem> ForRole(string role)
        {
            return role switch
            {
                RoleNames.Administrator =>
                [
                    new("/admin/dashboard", Screen.Dashboard, "grid", Screen.Overview),
                    new("/admin/equipment", Screen.EquipmentItems, "box", Screen.Inventory),
                    new("/admin/categories", Screen.EquipmentCategories, "tag"),
                    new("/admin/maintenance", Screen.Maintenance, "wrench"),
                    new("/admin/history", Screen.BorrowingHistory, "history", Screen.Reservations),
                    new("/admin/calendar", Screen.AvailabilityCalendar, "calendar"),
                    new("/admin/users", Screen.UsersRoles, "users", Screen.People),
                    new("/admin/audit", Screen.AuditLog, "audit"),
                ],
                RoleNames.Custodian =>
                [
                    new("/custodian/dashboard", Screen.Dashboard, "grid", Screen.Overview),
                    new("/custodian/approvals", Screen.ApprovalRelease, "check-circle", Screen.Desk),
                    new("/custodian/returns", Screen.ReturnConditionCheck, "undo"),
                    new("/custodian/history", Screen.BorrowingHistory, "history", Screen.Records),
                    new("/custodian/calendar", Screen.AvailabilityCalendar, "calendar"),
                    new("/admin/maintenance", Screen.Maintenance, "wrench"),
                    new("/custodian/categories", Screen.EquipmentCategories, "tag", Screen.Directory),
                    new("/custodian/borrowers", Screen.BorrowerProfiles, "users"),
                ],
                RoleNames.Borrower =>
                [
                    new("/borrower/home", Screen.Home, "home"),
                    new("/borrower/reservation", Screen.BrowseEquipment, "box"),
                    new("/borrower/history", Screen.MyReservations, "history"),
                    new("/borrower/calendar", Screen.Availability, "calendar"),
                ],
                _ => [],
            };
        }
    }
}
