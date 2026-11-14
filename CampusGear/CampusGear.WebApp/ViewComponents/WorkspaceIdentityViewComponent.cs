using System.Security.Claims;
using CampusGear.Services.Interfaces;
using CampusGear.WebApp.Models;
using Microsoft.AspNetCore.Mvc;

namespace CampusGear.WebApp.ViewComponents
{
    public sealed class WorkspaceIdentityViewComponent : ViewComponent
    {
        private readonly ICurrentAccountService _accounts;

        public WorkspaceIdentityViewComponent(ICurrentAccountService accounts)
        {
            _accounts = accounts;
        }

        public async Task<IViewComponentResult> InvokeAsync(string role, bool borrower)
        {
            var display = await _accounts.GetAsync(HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier), role);
            return View(new WorkspaceIdentityModel(display.Name, display.Initial, role, borrower));
        }
    }
}
