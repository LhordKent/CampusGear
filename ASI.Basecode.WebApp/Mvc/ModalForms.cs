using ASI.Basecode.WebApp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

// =====================================================================
// MODAL FORMS (server side)  -  the C# half of wwwroot/js/modal-forms.js.
// The JavaScript sends a header so the controller knows how to answer:
//   X-Modal: 1               -> send only the dialog's HTML (WantsModal)
//   X-Requested-With: fetch  -> answer with JSON {ok, message} instead of a page (IsFetch)
// =====================================================================
namespace ASI.Basecode.WebApp.Mvc
{
    /// <summary>
    /// Server side of js/modal-forms.js. An editor link opened with JavaScript asks for just its dialog (header X-Modal: 1),
    /// and the dialog's form saves with fetch (X-Requested-With: fetch) and expects JSON { ok, message, field }.
    /// Requests without those headers keep the plain page behaviour: redirect on success, the page re-rendered on error.
    /// </summary>
    internal static class ModalForms
    {
        // True when JavaScript asked for just the dialog (to open it over the current page).
        public static bool WantsModal(this HttpRequest request) => request.Headers["X-Modal"] == "1";

        // True when the form was posted by JavaScript and expects a JSON answer.
        public static bool IsFetch(this HttpRequest request) => request.Headers.XRequestedWith == "fetch";

        // JSON for "saved": the dialog closes and the message shows as a toast.
        public static JsonResult Saved(string? message) => new(ApiResult.CreateSuccess(message));

        /// <summary>Every error message in one line, with the first invalid field named so the dialog can focus it.</summary>
        // JSON for "not saved": the dialog stays open and shows the error(s).
        public static JsonResult Rejected(ModelStateDictionary state)
        {
            var failed = state.Where(x => x.Value?.Errors.Count > 0).ToList();
            var message = string.Join(
                " ",
                failed.SelectMany(x => x.Value!.Errors).Select(x => x.ErrorMessage).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct()
            );
            var field = failed.Select(x => x.Key).FirstOrDefault(x => x.Length > 0);
            return new(ApiResult.CreateError(message.Length > 0 ? message : "Check the highlighted field.", field))
            {
                StatusCode = 422,
            };
        }
    }
}
