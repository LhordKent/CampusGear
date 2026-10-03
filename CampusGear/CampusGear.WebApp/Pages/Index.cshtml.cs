using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CampusGear.Pages;

public class IndexModel : PageModel
{
    public IActionResult OnGet() => Redirect("/auth/login");
}
