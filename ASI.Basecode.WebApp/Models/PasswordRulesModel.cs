namespace ASI.Basecode.WebApp.Models
{
    /// <summary>Input for Views/Shared/_PasswordRules: which password (and optional confirm) field the checklist follows.</summary>
    public sealed record PasswordRulesModel(string PasswordId, string? ConfirmId = null, string Id = "password-help");
}
