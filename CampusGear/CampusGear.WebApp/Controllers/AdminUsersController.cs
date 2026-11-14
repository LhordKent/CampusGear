using System.Security.Claims;
using CampusGear.Models;
using CampusGear.Services;
using CampusGear.Services.ServiceModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace CampusGear.Controllers;
[Authorize(Roles = "Administrator")]
[AutoValidateAntiforgeryToken]
public sealed class AdminUsersController(IUserAdministrationService service) : Controller
{
    private RequestContext Actor => new(User.FindFirstValue(ClaimTypes.NameIdentifier), HttpContext.Connection.RemoteIpAddress?.ToString(), HttpContext.TraceIdentifier);
    [HttpGet("admin/borrowers")]
    public Task<IActionResult> Borrowers(string? q, string? status, int page = 1, CancellationToken cancellationToken = default) => Index(q, "Borrower", status, page, cancellationToken);
    [HttpGet("admin/users")]
    public async Task<IActionResult> Index(string? q, string? role, string? status, int page = 1, CancellationToken cancellationToken = default) =>
    View("Index", (await service.GetPageAsync(q, role, status, page, cancellationToken)) with { Feedback = TempData["AdminUsersFeedback"] as string, Error = TempData["AdminUsersError"] as string });
    [HttpPost("admin/users/{id}/approve")]
    public Task<IActionResult> Approve(string id, string? returnUrl, CancellationToken cancellationToken) => DecideRegistrationAsync(id, AccountApprovalStatus.Approved, returnUrl, cancellationToken);
    [HttpPost("admin/users/{id}/reject")]
    public Task<IActionResult> Reject(string id, string? returnUrl, CancellationToken cancellationToken) => DecideRegistrationAsync(id, AccountApprovalStatus.Rejected, returnUrl, cancellationToken);
    private async Task<IActionResult> DecideRegistrationAsync(string id, AccountApprovalStatus decision, string? returnUrl, CancellationToken ct)
    {
        var result = await service.DecideAsync(Actor, id, decision, ct);
        if (result.Outcome == OperationOutcome.NotFound) return NotFound();
        Feedback(result); return Redirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/admin/users?status=pending");
    }
    [HttpGet("admin/users/{id}/edit")]
    public Task<IActionResult> Edit(string id, CancellationToken cancellationToken) => EditViewAsync(id, null, cancellationToken);
    [HttpPost("admin/users/{id}/edit")]
    public async Task<IActionResult> Edit(string id, AdminUserEditInput input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return Request.IsFetch() ? ModalForms.Rejected(ModelState) : await EditViewAsync(id, input, cancellationToken);
        var result = await service.EditAsync(Actor, id, Command(input), cancellationToken);
        if (result.Outcome == OperationOutcome.NotFound) return NotFound();
        if (result.Outcome == OperationOutcome.Forbidden) return Forbid();
        if (result.Succeeded) return Request.IsFetch() ? ModalForms.Saved(result.Message) : SavedRedirect(result, "/admin/users");
        if (result.Reload) ModelState.Clear();
        Errors(result);
        return Request.IsFetch() ? ModalForms.Rejected(ModelState) : await EditViewAsync(id, result.Reload ? null : input, cancellationToken);
    }
    private async Task<IActionResult> EditViewAsync(string id, AdminUserEditInput? input, CancellationToken ct)
    {
        var page = await service.GetEditAsync(Actor, id, input is null ? null : Command(input), ct);
        if (page is null) return NotFound();
        return await EditorAsync("_EditAccount", new AdminUserEditPage(page.Id, page.Email, page.ApprovalStatus, page.ContactNumber, page.CurrentRole, page.HasBorrowerProfile, page.IsSelf,
        new AdminUserEditInput { FirstName = page.Input.FirstName, LastName = page.Input.LastName, Role = page.Input.Role, IsActive = page.Input.IsActive, StudentNumber = page.Input.StudentNumber, Department = page.Input.Department, IsEligible = page.Input.IsEligible, EligibilityNotes = page.Input.EligibilityNotes, ConcurrencyStamp = page.Input.ConcurrencyStamp, ProfileRowVersion = page.Input.ProfileRowVersion }), ct);
    }
    [HttpGet("admin/users/new")]
    public Task<IActionResult> Create(string? role, CancellationToken cancellationToken) =>
        EditorAsync("_CreateAccount", new AdminUserCreateInput { Role = role is "Borrower" or "Custodian" or "Administrator" ? role : "Borrower" }, cancellationToken);
    [HttpPost("admin/users/new")]
    public async Task<IActionResult> Create(AdminUserCreateInput input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return await CreateFailedAsync(input, cancellationToken);
        var result = await service.CreateAsync(Actor, new UserCreateCommand { FirstName = input.FirstName, LastName = input.LastName, Email = input.Email, Role = input.Role, Password = input.Password, StudentNumber = input.StudentNumber, Department = input.Department, ContactNumber = input.ContactNumber }, cancellationToken);
        if (!result.Succeeded) { Errors(result); return await CreateFailedAsync(input, cancellationToken); }
        return Request.IsFetch() ? ModalForms.Saved(result.Message) : SavedRedirect(result, input.Role == "Borrower" ? "/admin/borrowers" : "/admin/users");
    }
    private async Task<IActionResult> CreateFailedAsync(AdminUserCreateInput input, CancellationToken ct) =>
        Request.IsFetch() ? ModalForms.Rejected(ModelState) : await EditorAsync("_CreateAccount", input, ct);
    /// <summary>
    /// The account dialogs: only the dialog when JavaScript asks for it, otherwise the user list with the dialog
    /// open over it, so a bookmarked, refreshed, or script-less editor still looks the same.
    /// </summary>
    private async Task<IActionResult> EditorAsync(string editor, object model, CancellationToken ct)
    {
        if (Request.WantsModal()) return PartialView(editor, model);
        ViewData["Editor"] = editor;
        ViewData["EditorModel"] = model;
        return View("Index", await service.GetPageAsync(null, null, null, 1, ct));
    }
    private RedirectResult SavedRedirect(OperationResult<string> result, string url) { Feedback(result); return Redirect(url); }
    [HttpPost("admin/users/{id}/delete")]
    public async Task<IActionResult> Delete(string id, string? returnUrl, CancellationToken cancellationToken)
    {
        var result = await service.DeleteAsync(Actor, id, cancellationToken);
        if (result.Outcome == OperationOutcome.NotFound) return NotFound();
        Feedback(result); return Redirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/admin/users");
    }
    [HttpGet("admin/audit")]
    public async Task<IActionResult> Audit(string? q, int page = 1, CancellationToken cancellationToken = default) => View("Audit", await service.GetAuditAsync(q, page, cancellationToken));
    private void Feedback(OperationResult<string> result) => TempData[result.Succeeded ? "AdminUsersFeedback" : "AdminUsersError"] = result.Message;
    private void Errors(OperationResult<string> result)
    {
        if (result.Message is not null) ModelState.AddModelError("", result.Message);
        if (result.Errors is not null) foreach (var error in result.Errors) ModelState.AddModelError(error.Field, error.Message);
    }
    private static UserEditCommand Command(AdminUserEditInput input) => new() { FirstName = input.FirstName, LastName = input.LastName, Role = input.Role, IsActive = input.IsActive, StudentNumber = input.StudentNumber, Department = input.Department, IsEligible = input.IsEligible, EligibilityNotes = input.EligibilityNotes, ConcurrencyStamp = input.ConcurrencyStamp, ProfileRowVersion = input.ProfileRowVersion };
}
