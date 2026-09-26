using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CampusGear.Pages;

public sealed class ScreenModel : PageModel
{
    private readonly IWebHostEnvironment _environment;
    private static IReadOnlyList<ScreenEntry>? _catalog;

    public ScreenModel(IWebHostEnvironment environment) => _environment = environment;

    public string Title { get; private set; } = "CampusGear";
    public string Route { get; private set; } = "";
    public string State { get; private set; } = "default";
    public string NodeId { get; private set; } = "";
    public string ViewName { get; private set; } = "";

    public IActionResult OnGet(string workspace, string screen, string? state)
    {
        var requestedRoute = $"{workspace}/{screen}";
        var requestedState = string.IsNullOrWhiteSpace(state) ? "default" : state;
        var entry = GetCatalog().FirstOrDefault(x =>
            string.Equals(x.Route, requestedRoute, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.State, requestedState, StringComparison.OrdinalIgnoreCase));
        if (entry is null) return NotFound();

        Title = entry.Title;
        Route = entry.Route;
        State = entry.State;
        NodeId = entry.Id;
        ViewName = entry.ViewName;
        return Page();
    }

    private IReadOnlyList<ScreenEntry> GetCatalog()
    {
        if (_catalog is not null) return _catalog;
        var path = Path.Combine(_environment.WebRootPath, "figma", "catalog.json");
        var json = System.IO.File.ReadAllText(path);
        _catalog = JsonSerializer.Deserialize<List<ScreenEntry>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("The Figma screen catalog is empty.");
        return _catalog;
    }

    private sealed record ScreenEntry(string Id, string Title, string Section, string Route, string State, string Url, string ViewName);
}
