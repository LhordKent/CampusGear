using System.Security.Claims;
using CampusGear.Services;
using CampusGear.Models;
using Microsoft.AspNetCore.Mvc;
namespace CampusGear.ViewComponents;
public sealed class WorkspaceIdentityViewComponent(ICurrentAccountService accounts) : ViewComponent
{
 public async Task<IViewComponentResult> InvokeAsync(string role,bool borrower)
 {
 var display=await accounts.GetAsync(HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier),role);
 return View(new WorkspaceIdentityModel(display.Name,display.Initial,role,borrower));
 }
}
