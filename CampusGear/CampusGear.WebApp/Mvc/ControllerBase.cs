using System.Security.Claims;
using AutoMapper;
using CampusGear.Services.ServiceModels;
using Microsoft.AspNetCore.Mvc;

namespace CampusGear.WebApp.Mvc
{
    /// <summary>Shared MVC infrastructure. Business decisions belong to injected services.</summary>
    public abstract class ControllerBase<TController> : Controller
    {
        protected ILogger<TController> Logger { get; }

        protected IConfiguration Configuration { get; }

        protected IMapper Mapper { get; }

        protected IHttpContextAccessor ContextAccessor { get; }

        protected ControllerBase(
            IHttpContextAccessor httpContextAccessor,
            ILoggerFactory loggerFactory,
            IConfiguration configuration,
            IMapper mapper
        )
        {
            ContextAccessor = httpContextAccessor;
            Logger = loggerFactory.CreateLogger<TController>();
            Configuration = configuration;
            Mapper = mapper;
        }

        protected RequestContext RequestActor =>
            new(
                User.Identity?.IsAuthenticated == true ? User.FindFirstValue(ClaimTypes.NameIdentifier) : null,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                HttpContext.TraceIdentifier
            );

        protected void ShowFeedback<T>(OperationResult<T> result, string? notice = null)
        {
            if (result.Message is not null)
            {
                ViewData["ErrorMessage"] = result.Message;
            }

            if (notice is not null)
            {
                ViewData["NoticeMessage"] = notice;
            }

            foreach (var error in result.Errors ?? [])
            {
                ModelState.AddModelError(error.Field, error.Message);
            }
        }

        protected void StoreFeedback<T>(OperationResult<T> result, string? notice = null)
        {
            if (result.Message is not null)
            {
                TempData["ErrorMessage"] = result.Message;
            }

            if (notice is not null)
            {
                TempData["NoticeMessage"] = notice;
            }
        }

        protected void RestoreFeedback()
        {
            if (TempData["ErrorMessage"] is string error)
            {
                ViewData["ErrorMessage"] = error;
            }

            if (TempData["NoticeMessage"] is string notice)
            {
                ViewData["NoticeMessage"] = notice;
            }
        }
    }
}
