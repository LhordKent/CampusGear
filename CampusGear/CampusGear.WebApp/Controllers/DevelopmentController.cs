using System.Net;
using CampusGear.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CampusGear.Controllers;

public sealed class DevelopmentController(IWebHostEnvironment environment, IServiceProvider services) : Controller
{
    [AllowAnonymous]
    [HttpGet("development/email")]
    public IActionResult Email()
    {
        var address = HttpContext.Connection.RemoteIpAddress;
        if (!environment.IsDevelopment() || address is null || !IPAddress.IsLoopback(address)) return NotFound();
        Response.Headers.CacheControl = "no-store";
        var mailbox = services.GetService<DevelopmentEmailSender>();
        if (mailbox is null) return NotFound();
        return View(mailbox.GetRecent());
    }
}
