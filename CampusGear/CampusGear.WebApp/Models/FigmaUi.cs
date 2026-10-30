namespace CampusGear.Models;

public sealed record FigmaEditorValue(string Label, string Value, string? InputId = null);
public sealed record FigmaEditorFooter(string FormId, string CancelUrl, IReadOnlyList<FigmaEditorValue> Values);

public sealed record FigmaNavigation(string Path, string Label, string Icon);

public static class FigmaUi
{
    public static IReadOnlyList<FigmaNavigation> Navigation(string role) => role switch
    {
        // Icon values are symbol ids in wwwroot/icons.svg.
        "Administrator" => [
            new("/admin/dashboard", "Dashboard", "home"),
            new("/admin/categories", "Categories", "layers"),
            new("/admin/equipment", "Equipment", "box"),
            new("/admin/borrowers", "Borrowers", "users"),
            new("/admin/users", "Users & Roles", "shield"),
            new("/admin/history", "Borrowing History", "history"),
            new("/admin/calendar", "Calendar", "calendar"),
            new("/admin/audit", "Audit Log", "file")],
        "Custodian" => [
            new("/custodian/dashboard", "Dashboard", "home"),
            new("/custodian/approvals", "Approvals", "check-circle"),
            new("/custodian/returns", "Returns", "undo"),
            new("/custodian/calendar", "Calendar", "calendar")],
        _ => [
            new("/borrower/dashboard", "Dashboard", "home"),
            new("/borrower/reservation", "Reserve Equipment", "calendar-plus"),
            new("/borrower/history", "My History", "history"),
            new("/borrower/calendar", "Calendar", "calendar")]
    };
    public static string Frame(string path) => path.ToLowerInvariant() switch
    {
        "/borrower/dashboard" => "689:2", "/borrower/reservation" => "695:2",
        "/borrower/history" => "695:562", "/borrower/calendar" => "695:1234",
        "/custodian/dashboard" => "690:8", "/custodian/approvals" => "695:1499",
        "/custodian/returns" => "695:2032", "/custodian/calendar" => "695:2691",
        "/admin/dashboard" => "690:212", "/admin/reservations" => "695:2931",
        "/admin/approvals" => "695:3278", "/admin/returns" => "695:3957",
        "/admin/calendar" => "695:4296", "/admin/categories" => "695:4991",
        "/admin/equipment" => "695:5691", "/admin/borrowers" => "695:6398",
        "/admin/users" => "695:7067", "/admin/audit" => "945:195",
        "/admin/history" => "695:4595", _ => ""
    };
}
