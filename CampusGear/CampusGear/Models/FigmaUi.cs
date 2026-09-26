namespace CampusGear.Models;

public sealed record FigmaEditorValue(string Label, string Value, string? InputId = null);
public sealed record FigmaEditorFooter(string FormId, string CancelUrl, IReadOnlyList<FigmaEditorValue> Values);

public sealed record FigmaNavigation(string Path, string Label, string Icon);

public static class FigmaUi
{
    public static IReadOnlyList<FigmaNavigation> Navigation(string role) => role switch
    {
        "Administrator" => [
            new("/admin/dashboard", "Dashboard", "edd56.svg"),
            new("/admin/categories", "Equipment Categories", "7e0b6.svg"),
            new("/admin/equipment", "Equipment Items", "9177b.svg"),
            new("/admin/borrowers", "Borrower Profiles", "a40a1.svg"),
            new("/admin/users", "User & Role Management", "be9c1.svg"),
            new("/admin/history", "Borrowing History", "e1db6.svg"),
            new("/admin/calendar", "Availability Calendar", "9f767.svg"),
            new("/admin/audit", "System Audit Log", "bc2d0.svg")],
        "Custodian" => [
            new("/custodian/dashboard", "Dashboard", "edd56.svg"),
            new("/custodian/approvals", "Approval & Release", "e7a8d.svg"),
            new("/custodian/returns", "Return & Condition Check", "9970e.svg"),
            new("/custodian/calendar", "Availability Calendar", "9f767.svg")],
        _ => [
            new("/borrower/dashboard", "Dashboard", "edd56.svg"),
            new("/borrower/reservation", "Equipment Reservation", "399fa.svg"),
            new("/borrower/history", "Borrowing History", "e1db6.svg"),
            new("/borrower/calendar", "Availability Calendar", "9f767.svg")]
    };
    public static string Atmosphere(string role) => role switch
    { "Administrator" => "e0141.svg", "Custodian" => "faa7e.svg", _ => "8fc42.svg" };
    public static string Detail(string role) => role switch
    { "Administrator" => "7a4b9.svg", "Custodian" => "da63d.svg", _ => "646e8.svg" };
    public static string Signature(string role) => role == "Administrator" ? "44f3c.svg" : "35f2a.svg";
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
