using System.Security.Claims;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.WebApp.Models;
using Microsoft.AspNetCore.Mvc;

namespace ASI.Basecode.WebApp.Mvc
{
    /// <summary>Renders the signed-in user's name and initial (Views/Shared/_LoginUser.cshtml).</summary>
    public sealed class LoginUserViewComponent : ViewComponent
    {
        private readonly ICurrentAccountService _accounts;

        public LoginUserViewComponent(ICurrentAccountService accounts)
        {
            _accounts = accounts;
        }

        public async Task<IViewComponentResult> InvokeAsync(string role, bool borrower)
        {
            var display = await _accounts.GetAsync(HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier), role);
            return View("~/Views/Shared/_LoginUser.cshtml", new LoginUser(display.Name, display.Initial, role, borrower));
        }
    }
}
