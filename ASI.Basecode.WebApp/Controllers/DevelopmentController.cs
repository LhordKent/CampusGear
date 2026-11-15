using System.Net;
using ASI.Basecode.Services.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ASI.Basecode.WebApp.Controllers
{
    public sealed class DevelopmentController : Controller
    {
        private readonly IWebHostEnvironment _environment;
        private readonly DevelopmentEmailSender? _mailbox;

        public DevelopmentController(IWebHostEnvironment environment, DevelopmentEmailSender? mailbox = null)
        {
            _environment = environment;
            _mailbox = mailbox;
        }

        [AllowAnonymous]
        [HttpGet("development/email")]
        public IActionResult Email()
        {
            var address = HttpContext.Connection.RemoteIpAddress;
            if (!_environment.IsDevelopment() || address is null || !IPAddress.IsLoopback(address))
            {
                return NotFound();
            }

            Response.Headers.CacheControl = "no-store";
            var mailbox = _mailbox;
            if (mailbox is null)
            {
                return NotFound();
            }

            return View(mailbox.GetRecent());
        }

        [AllowAnonymous, HttpGet("auth/development-inbox")]
        public IActionResult DevelopmentInbox()
        {
            var address = HttpContext.Connection.RemoteIpAddress;
            if (!_environment.IsDevelopment() || address is null || !IPAddress.IsLoopback(address) || _mailbox is null)
            {
                return NotFound();
            }

            Response.Headers.CacheControl = "no-store";
            return Json(_mailbox.GetRecent());
        }
    }
}
