namespace CampusGear.Models;

public sealed record FigmaEditorFooter(string FormId, string CancelUrl);

/// <summary>A workspace navigation link. Icon is a symbol id in wwwroot/icons.svg; Group starts a labelled section.</summary>
public sealed record FigmaNavigation(string Path, string Label, string Icon, string? Group = null);

public static class FigmaUi
{
    public static IReadOnlyList<FigmaNavigation> Navigation(string role) => role switch
    {
        "Administrator" => [
            new("/admin/dashboard", "Dashboard", "grid", "Overview"),
            new("/admin/equipment", "Equipment Items", "box", "Inventory"),
            new("/admin/categories", "Equipment Categories", "tag"),
            new("/admin/maintenance", "Maintenance", "wrench"),
            new("/admin/history", "Borrowing History", "history", "Reservations"),
            new("/admin/calendar", "Availability Calendar", "calendar"),
            new("/admin/borrowers", "Borrower Profiles", "users", "People"),
            new("/admin/users", "Users & Roles", "shield"),
            new("/admin/audit", "Audit Log", "audit")],
        "Custodian" => [
            new("/custodian/dashboard", "Dashboard", "grid", "Overview"),
            new("/custodian/approvals", "Approval & Release", "check-circle", "Desk"),
            new("/custodian/returns", "Return & Condition Check", "undo"),
            new("/custodian/history", "Borrowing History", "history", "Records"),
            new("/custodian/calendar", "Availability Calendar", "calendar"),
            new("/admin/maintenance", "Maintenance", "wrench"),
            new("/custodian/categories", "Equipment Categories", "tag", "Directory"),
            new("/custodian/borrowers", "Borrower Profiles", "users")],
        _ => [
            new("/borrower/home", "Home", "home"),
            new("/borrower/reservation", "Browse Equipment", "box"),
            new("/borrower/history", "My Reservations", "history"),
            new("/borrower/calendar", "Availability", "calendar")]
    };
    public static string Atmosphere(string role) => role switch
    { "Administrator" => "e0141.svg", "Custodian" => "faa7e.svg", _ => "8fc42.svg" };
    public static string Detail(string role) => role switch
    { "Administrator" => "7a4b9.svg", "Custodian" => "da63d.svg", _ => "646e8.svg" };
    public static string Signature(string role) => role == "Administrator" ? "44f3c.svg" : "35f2a.svg";
    public static string Frame(string path) => path.ToLowerInvariant() switch
    {
        "/borrower/dashboard" or "/borrower/home" => "689:2", "/borrower/reservation" => "695:2",
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
