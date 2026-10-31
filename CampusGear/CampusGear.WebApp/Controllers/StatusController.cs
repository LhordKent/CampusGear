using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CampusGear.Controllers;

/// <summary>
/// Renders the body for error status codes that reach the status-code pages middleware (Program.cs). The status code
/// itself is never changed: a 403 stays 403. A browser navigating to a forbidden page gets the access-denied page with
/// that 403; background requests, form posts and every other code get the bare status, as before.
/// </summary>
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class StatusController : Controller
{
    [Route("status/{code:int}")]
    public IActionResult Show(int code)
    {
        if (code == StatusCodes.Status403Forbidden && AuthResponses.IsPageNavigation(Request))
            return new ViewResult { ViewName = "~/Views/Account/AccessDenied.cshtml", StatusCode = code };
        return StatusCode(code);
    }
}

public static class AuthResponses
{
    /// <summary>A browser loading a page, as opposed to fetch/XHR calls and form posts.</summary>
    public static bool IsPageNavigation(HttpRequest request) =>
        (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method)) &&
        !request.Headers.ContainsKey("X-Requested-With") &&
        request.Headers.Accept.ToString().Contains("text/html", StringComparison.OrdinalIgnoreCase);
}
