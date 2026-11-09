using System.Text.Json;
using System.Text.RegularExpressions;
using CampusGear.Services;
namespace CampusGear.IntegrationTests;

public sealed class DeveloperDocumentationTests
{
    private static string Root
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "docs"))) dir = dir.Parent;
            return dir?.FullName ?? throw new InvalidOperationException("Source checkout required for documentation checks.");
        }
    }
    [Fact]
    public void Maintained_source_catalog_covers_every_application_owned_file()
    {
        var catalog = File.ReadAllText(Path.Combine(Root, "docs/SOURCE_CATALOG.md"));
        foreach (var file in Directory.EnumerateFiles(Path.Combine(Root, "CampusGear"), "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(Root, file).Replace('\\', '/');
            if (rel.Split('/').Any(x => x is "bin" or "obj" or "lib" or "Migrations" or "Frames")) continue;
            if (!new[] { ".cs", ".cshtml", ".js", ".css" }.Contains(Path.GetExtension(file))) continue;
            if (Path.GetFileName(file) == "figma-generated.css") continue;
            Assert.Contains("](" + ("../" + rel) + ")", catalog);
        }
    }
    [Fact]
    public void Current_developer_guides_have_resolvable_local_links()
    {
        var files = Directory.EnumerateFiles(Path.Combine(Root, "docs"), "*.md")
        .Append(Path.Combine(Root, "README.md")).Append(Path.Combine(Root, "scripts/verification.md"));
        foreach (var file in files)
            foreach (Match match in Regex.Matches(File.ReadAllText(file), @"\[[^\]]*\]\(([^)]+)\)"))
            {
                var target = match.Groups[1].Value;
                if (target.StartsWith("http") || target.StartsWith("#")) continue;
                target = target.Split('#')[0];
                var path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file)!, Uri.UnescapeDataString(target)));
                Assert.True(File.Exists(path) || Directory.Exists(path), file + " has broken link " + target);
            }
    }
    [Fact]
    public void Trace_map_names_real_application_methods()
    {
        var text = File.ReadAllText(Path.Combine(Root, "docs/ROUTE_MIGRATION_MAP.md"));
        foreach (Match match in Regex.Matches(text, @"\x60(\w+Service)\.(\w+)\x60"))
        {
            var type = typeof(ICategoryService).Assembly.GetTypes().SingleOrDefault(x => x.Name == match.Groups[1].Value);
            Assert.NotNull(type);
            Assert.NotEmpty(type!.GetMethods().Where(x => x.Name == match.Groups[2].Value));
        }
    }
    [Fact]
    public void Route_spellings_and_role_attribute_inventory_match_frozen_main_baseline()
    {
        var snapshot = JsonSerializer.Deserialize<List<HttpContract>>(File.ReadAllText(Path.Combine(Root, "docs/baseline-http-contracts.json")))!;
        foreach (var entry in snapshot)
        {
            var source = File.ReadAllText(Path.Combine(Root, entry.File));
            var prefix = Path.GetFileName(entry.File) == "AccountController.cs" ? "auth/" : "";
            var routes = Regex.Matches(source, "Http(Get|Post)\\(\"([^\"]+)\"").Select(m => m.Groups[1].Value.ToUpperInvariant() + " /" + prefix + m.Groups[2].Value).Order().ToArray();
            var roles = Regex.Matches(source, "Authorize\\(Roles\\s*=\\s*\"([^\"]+)\"").Select(m => m.Groups[1].Value).Order().ToArray();
            Assert.Equal(entry.Routes.Order(), routes); Assert.Equal(entry.Roles.Order(), roles);
        }
    }
    private sealed record HttpContract(string File, string[] Routes, string[] Roles);
}
