using AutoMapper;
using CampusGear.Services.Interfaces;
using CampusGear.Services.ServiceModels;
using CampusGear.WebApp.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CampusGear.WebApp.Controllers
{
    [Route("auth")]
    [AutoValidateAntiforgeryToken]
    [TypeFilter(typeof(AccountAntiforgeryRecoveryFilter))]
    public sealed class RegistrationController : Mvc.ControllerBase<RegistrationController>
    {
        private readonly IRegistrationService _registrationService;

        public RegistrationController(
            IHttpContextAccessor httpContextAccessor,
            ILoggerFactory loggerFactory,
            IConfiguration configuration,
            IMapper mapper,
            IRegistrationService registrationService
        )
            : base(httpContextAccessor, loggerFactory, configuration, mapper)
        {
            _registrationService = registrationService;
        }

        [AllowAnonymous, HttpGet("signup")]
        public IActionResult SignUp()
        {
            RestoreFeedback();
            return View("~/Views/Account/SignUp.cshtml");
        }

        [AllowAnonymous, EnableRateLimiting("auth-post"), HttpPost("signup")]
        public async Task<IActionResult> SignUp([FromForm] SignUpInput input)
        {
            if (ModelState.IsValid)
            {
                var result = await _registrationService.SignUpAsync(RequestActor, input, HttpContext.RequestAborted);
                if (result.Value!.Status == RegistrationStatus.Submitted)
                {
                    return RedirectToAction(nameof(RegistrationSubmitted));
                }

                ShowFeedback(result);
            }

            ViewData["FirstName"] = input.FirstName;
            ViewData["LastName"] = input.LastName;
            ViewData["StudentIdNumber"] = input.StudentIdNumber;
            ViewData["Email"] = input.Email;
            ViewData["MobileNumber"] = input.MobileNumber;
            return View("~/Views/Account/SignUp.cshtml");
        }

        [AllowAnonymous, HttpGet("registration-submitted")]
        public IActionResult RegistrationSubmitted()
        {
            return View("~/Views/Account/RegistrationSubmitted.cshtml");
        }
    }
}
