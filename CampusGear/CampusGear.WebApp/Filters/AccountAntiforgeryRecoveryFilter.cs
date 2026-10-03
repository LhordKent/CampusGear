using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Core.Infrastructure;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace CampusGear.Filters;

public sealed class AccountAntiforgeryRecoveryFilter(
    ITempDataDictionaryFactory tempDataFactory,
    ILogger<AccountAntiforgeryRecoveryFilter> logger) : IAlwaysRunResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is not IAntiforgeryValidationFailedResult) return;

        // Validation has already rejected the POST. Return a fresh GET form;
        // never retry the submitted action or carry its password/code forward.
        var path = context.HttpContext.Request.Path.Value?.ToLowerInvariant();
        var destination = path switch
        {
            "/auth/signup" => "/auth/signup",
            "/auth/email-verification" or "/auth/resend-email-verification" => "/auth/email-verification",
            "/auth/two-factor" or "/auth/resend-two-factor" => "/auth/two-factor",
            "/auth/password-reset-request" => "/auth/password-reset-request",
            "/auth/password-reset-code" or "/auth/resend-password-reset-code" => "/auth/password-reset-code",
            "/auth/password-reset-new" => "/auth/password-reset-new",
            _ => "/auth/login"
        };
        var tempData = tempDataFactory.GetTempData(context.HttpContext);
        tempData["ErrorMessage"] =
            "Your form is out of date. Please enter your details and try again.";
        // Authorization short-circuited MVC before its usual TempData save filter.
        tempData.Save();
        context.HttpContext.Response.Headers.CacheControl = "no-store";
        logger.LogInformation("Rejected an invalid account form token for {Path}; returning a fresh form.", path);
        context.Result = new RedirectResult(destination);
    }

    public void OnResultExecuted(ResultExecutedContext context) { }
}
