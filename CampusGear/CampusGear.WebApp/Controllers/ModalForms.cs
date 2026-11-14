using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CampusGear.Controllers;

/// <summary>
/// Server side of js/modal-forms.js. An editor link opened with JavaScript asks for just its dialog (header X-Modal: 1),
/// and the dialog's form saves with fetch (X-Requested-With: fetch) and expects JSON { ok, message, field }.
/// Requests without those headers keep the plain page behaviour: redirect on success, the page re-rendered on error.
/// </summary>
internal static class ModalForms
{
    public static bool WantsModal(this HttpRequest request) => request.Headers["X-Modal"] == "1";
    public static bool IsFetch(this HttpRequest request) => request.Headers.XRequestedWith == "fetch";

    public static JsonResult Saved(string? message) => new(new { ok = true, message });

    /// <summary>Every error message in one line, with the first invalid field named so the dialog can focus it.</summary>
    public static JsonResult Rejected(ModelStateDictionary state)
    {
        var failed = state.Where(x => x.Value?.Errors.Count > 0).ToList();
        var message = string.Join(" ", failed.SelectMany(x => x.Value!.Errors).Select(x => x.ErrorMessage)
            .Where(x => !string.IsNullOrWhiteSpace(x)).Distinct());
        var field = failed.Select(x => x.Key).FirstOrDefault(x => x.Length > 0);
        return new(new { ok = false, message = message.Length > 0 ? message : "Check the highlighted field.", field }) { StatusCode = 422 };
    }
}
