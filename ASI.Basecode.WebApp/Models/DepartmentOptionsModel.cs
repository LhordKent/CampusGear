namespace ASI.Basecode.WebApp.Models
{
    /// <summary>
    /// Options for a department &lt;select&gt; (Views/Shared/_DepartmentOptions.cshtml).
    /// </summary>
    /// <param name="Selected">The current value. A saved value that is no longer listed is still offered, so editing never drops it.</param>
    /// <param name="Placeholder">Text of the empty first option.</param>
    /// <param name="AllowEmpty">True when "no department" is a valid choice (admin forms); false makes the placeholder unselectable (sign-up).</param>
    public sealed record DepartmentOptionsModel(string? Selected, string Placeholder, bool AllowEmpty);
}
