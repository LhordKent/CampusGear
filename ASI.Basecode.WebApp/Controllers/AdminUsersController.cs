using AutoMapper;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.WebApp.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ASI.Basecode.WebApp.Controllers
{
    [Authorize(Roles = RoleNames.Administrator)]
    [AutoValidateAntiforgeryToken]
    public sealed class AdminUsersController : Mvc.ControllerBase<AdminUsersController>
    {
        private readonly IUserService _service;
        private readonly IAuditService _audit;

        public AdminUsersController(
            IHttpContextAccessor httpContextAccessor,
            ILoggerFactory loggerFactory,
            IConfiguration configuration,
            IMapper mapper,
            IUserService service,
            IAuditService audit
        )
            : base(httpContextAccessor, loggerFactory, configuration, mapper)
        {
            _service = service;
            _audit = audit;
        }

        [HttpGet("admin/borrowers")]
        public Task<IActionResult> Borrowers(string? q, string? status, int page = 1, CancellationToken cancellationToken = default)
        {
            return Index(q, RoleNames.Borrower, status, page, cancellationToken);
        }

        [HttpGet("admin/users")]
        public async Task<IActionResult> Index(
            string? q,
            string? role,
            string? status,
            int page = 1,
            CancellationToken cancellationToken = default
        )
        {
            return View(
                "Index",
                (await _service.GetPageAsync(q, role, status, page, cancellationToken)) with
                {
                    Feedback = TempData["AdminUsersFeedback"] as string,
                    Error = TempData["AdminUsersError"] as string,
                }
            );
        }

        [HttpPost("admin/users/{id}/approve")]
        public Task<IActionResult> Approve(string id, string? returnUrl, CancellationToken cancellationToken)
        {
            return DecideRegistrationAsync(id, AccountApprovalStatus.Approved, returnUrl, cancellationToken);
        }

        [HttpPost("admin/users/{id}/reject")]
        public Task<IActionResult> Reject(string id, string? returnUrl, CancellationToken cancellationToken)
        {
            return DecideRegistrationAsync(id, AccountApprovalStatus.Rejected, returnUrl, cancellationToken);
        }

        private async Task<IActionResult> DecideRegistrationAsync(
            string id,
            AccountApprovalStatus decision,
            string? returnUrl,
            CancellationToken ct
        )
        {
            var result = await _service.DecideAsync(RequestActor, id, decision, ct);
            if (result.Outcome == OperationOutcome.NotFound)
            {
                return NotFound();
            }

            Feedback(result);
            return Redirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/admin/users?status=pending");
        }

        [HttpGet("admin/users/{id}/edit")]
        public Task<IActionResult> Edit(string id, CancellationToken cancellationToken)
        {
            return EditViewAsync(id, null, cancellationToken);
        }

        [HttpPost("admin/users/{id}/edit")]
        public async Task<IActionResult> Edit(string id, UserEditModel input, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return Request.IsFetch() ? ModalForms.Rejected(ModelState) : await EditViewAsync(id, input, cancellationToken);
            }

            var result = await _service.EditAsync(RequestActor, id, input, cancellationToken);
            if (result.Outcome == OperationOutcome.NotFound)
            {
                return NotFound();
            }

            if (result.Outcome == OperationOutcome.Forbidden)
            {
                return Forbid();
            }

            if (result.Succeeded)
            {
                return Request.IsFetch() ? ModalForms.Saved(result.Message) : SavedRedirect(result, "/admin/users");
            }

            if (result.Reload)
            {
                ModelState.Clear();
            }

            Errors(result);
            return Request.IsFetch()
                ? ModalForms.Rejected(ModelState)
                : await EditViewAsync(id, result.Reload ? null : input, cancellationToken);
        }

        private async Task<IActionResult> EditViewAsync(string id, UserEditModel? input, CancellationToken ct)
        {
            var page = await _service.GetEditAsync(RequestActor, id, input, ct);
            if (page is null)
            {
                return NotFound();
            }

            return await EditorAsync("_EditAccount", page, ct);
        }

        [HttpGet("admin/users/new")]
        public Task<IActionResult> Create(string? role, CancellationToken cancellationToken)
        {
            return EditorAsync(
                "_CreateAccount",
                new UserCreateModel
                {
                    Role = role is RoleNames.Borrower or RoleNames.Custodian or RoleNames.Administrator ? role : RoleNames.Borrower,
                },
                cancellationToken
            );
        }

        [HttpPost("admin/users/new")]
        public async Task<IActionResult> Create(UserCreateModel input, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return await CreateFailedAsync(input, cancellationToken);
            }

            var result = await _service.CreateAsync(RequestActor, input, cancellationToken);
            if (!result.Succeeded)
            {
                Errors(result);
                return await CreateFailedAsync(input, cancellationToken);
            }

            return Request.IsFetch()
                ? ModalForms.Saved(result.Message)
                : SavedRedirect(result, input.Role == RoleNames.Borrower ? "/admin/borrowers" : "/admin/users");
        }

        private async Task<IActionResult> CreateFailedAsync(UserCreateModel input, CancellationToken ct)
        {
            return Request.IsFetch() ? ModalForms.Rejected(ModelState) : await EditorAsync("_CreateAccount", input, ct);
        }

        /// <summary>
        /// The account dialogs: only the dialog when JavaScript asks for it, otherwise the user list with the dialog
        /// open over it, so a bookmarked, refreshed, or script-less editor still looks the same.
        /// </summary>
        private async Task<IActionResult> EditorAsync(string editor, object model, CancellationToken ct)
        {
            if (Request.WantsModal())
            {
                return PartialView(editor, model);
            }

            ViewData["Editor"] = editor;
            ViewData["EditorModel"] = model;
            return View("Index", await _service.GetPageAsync(null, null, null, 1, ct));
        }

        private RedirectResult SavedRedirect(OperationResult<string> result, string url)
        {
            Feedback(result);
            return Redirect(url);
        }

        [HttpPost("admin/users/{id}/delete")]
        public async Task<IActionResult> Delete(string id, string? returnUrl, CancellationToken cancellationToken)
        {
            var result = await _service.DeleteAsync(RequestActor, id, cancellationToken);
            if (result.Outcome == OperationOutcome.NotFound)
            {
                return NotFound();
            }

            Feedback(result);
            return Redirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/admin/users");
        }

        [HttpGet("admin/audit")]
        public async Task<IActionResult> Audit(string? q, int page = 1, CancellationToken cancellationToken = default)
        {
            return View("Audit", await _audit.GetAuditAsync(q, page, cancellationToken));
        }

        private void Feedback(OperationResult<string> result)
        {
            TempData[result.Succeeded ? "AdminUsersFeedback" : "AdminUsersError"] = result.Message;
        }

        private void Errors(OperationResult<string> result)
        {
            if (result.Message is not null)
            {
                ModelState.AddModelError("", result.Message);
            }

            if (result.Errors is not null)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(error.Field, error.Message);
                }
            }
        }
    }
}
