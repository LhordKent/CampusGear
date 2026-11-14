using CampusGear.Resources.Constants;
using CampusGear.Resources.Views;

namespace CampusGear.WebApp.Models
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
                    new("/admin/dashboard", WorkspaceLabels.Dashboard, "grid", WorkspaceLabels.Overview),
                    new("/admin/equipment", WorkspaceLabels.EquipmentItems, "box", WorkspaceLabels.Inventory),
                    new("/admin/categories", WorkspaceLabels.EquipmentCategories, "tag"),
                    new("/admin/maintenance", WorkspaceLabels.Maintenance, "wrench"),
                    new("/admin/history", WorkspaceLabels.BorrowingHistory, "history", WorkspaceLabels.Reservations),
                    new("/admin/calendar", WorkspaceLabels.AvailabilityCalendar, "calendar"),
                    new("/admin/borrowers", WorkspaceLabels.BorrowerProfiles, "users", WorkspaceLabels.People),
                    new("/admin/users", WorkspaceLabels.UsersRoles, "shield"),
                    new("/admin/audit", WorkspaceLabels.AuditLog, "audit"),
                ],
                RoleNames.Custodian =>
                [
                    new("/custodian/dashboard", WorkspaceLabels.Dashboard, "grid", WorkspaceLabels.Overview),
                    new("/custodian/approvals", WorkspaceLabels.ApprovalRelease, "check-circle", WorkspaceLabels.Desk),
                    new("/custodian/returns", WorkspaceLabels.ReturnConditionCheck, "undo"),
                    new("/custodian/history", WorkspaceLabels.BorrowingHistory, "history", WorkspaceLabels.Records),
                    new("/custodian/calendar", WorkspaceLabels.AvailabilityCalendar, "calendar"),
                    new("/admin/maintenance", WorkspaceLabels.Maintenance, "wrench"),
                    new("/custodian/categories", WorkspaceLabels.EquipmentCategories, "tag", WorkspaceLabels.Directory),
                    new("/custodian/borrowers", WorkspaceLabels.BorrowerProfiles, "users"),
                ],
                RoleNames.Borrower =>
                [
                    new("/borrower/home", WorkspaceLabels.Home, "home"),
                    new("/borrower/reservation", WorkspaceLabels.BrowseEquipment, "box"),
                    new("/borrower/history", WorkspaceLabels.MyReservations, "history"),
                    new("/borrower/calendar", WorkspaceLabels.Availability, "calendar"),
                ],
                _ => [],
            };
        }
    }
}
