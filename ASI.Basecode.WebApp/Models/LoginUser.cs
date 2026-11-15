namespace ASI.Basecode.WebApp.Models
{
    /// <summary>The signed-in user shown in the workspace header or sidebar (Views/Shared/_LoginUser.cshtml).</summary>
    public sealed record LoginUser(string Name, string Initial, string Role, bool Borrower);
}
