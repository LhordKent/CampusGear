using CampusGear.Resources.Constants;

namespace CampusGear.WebApp.Models
{
    public static class FigmaReference
    {
        public static string Atmosphere(string role)
        {
            return role switch
            {
                RoleNames.Administrator => "e0141.svg",
                RoleNames.Custodian => "faa7e.svg",
                _ => "8fc42.svg",
            };
        }

        public static string Detail(string role)
        {
            return role switch
            {
                RoleNames.Administrator => "7a4b9.svg",
                RoleNames.Custodian => "da63d.svg",
                _ => "646e8.svg",
            };
        }

        public static string Signature(string role) => role == RoleNames.Administrator ? "44f3c.svg" : "35f2a.svg";

        public static string Frame(string path)
        {
            return path.ToLowerInvariant() switch
            {
                "/borrower/dashboard" or "/borrower/home" => "689:2",
                "/borrower/reservation" => "695:2",
                "/borrower/history" => "695:562",
                "/borrower/calendar" => "695:1234",
                "/custodian/dashboard" => "690:8",
                "/custodian/approvals" => "695:1499",
                "/custodian/returns" => "695:2032",
                "/custodian/calendar" => "695:2691",
                "/admin/dashboard" => "690:212",
                "/admin/reservations" => "695:2931",
                "/admin/approvals" => "695:3278",
                "/admin/returns" => "695:3957",
                "/admin/calendar" => "695:4296",
                "/admin/categories" => "695:4991",
                "/admin/equipment" => "695:5691",
                "/admin/borrowers" => "695:6398",
                "/admin/users" => "695:7067",
                "/admin/audit" => "945:195",
                "/admin/history" => "695:4595",
                _ => "",
            };
        }
    }
}
