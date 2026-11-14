using System.Text.RegularExpressions;
using CampusGear.Services.Interfaces;

namespace CampusGear.IntegrationTests
{
    public sealed class DeveloperDocumentationTests
    {
        private static string Root
        {
            get
            {
                var dir = new DirectoryInfo(AppContext.BaseDirectory);
                while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "docs")))
                {
                    dir = dir.Parent;
                }

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
                if (rel.Split('/').Any(x => x is "bin" or "obj" or "lib" or "Migrations" or "Frames"))
                {
                    continue;
                }

                if (!new[] { ".cs", ".cshtml", ".js", ".css", ".resx", ".csproj" }.Contains(Path.GetExtension(file)))
                {
                    continue;
                }

                if (Path.GetFileName(file) == "figma-generated.css")
                {
                    continue;
                }

                Assert.Contains("](" + ("../" + rel) + ")", catalog);
            }
        }

        [Fact]
        public void Current_developer_guides_have_resolvable_local_links()
        {
            var files = Directory
                .EnumerateFiles(Path.Combine(Root, "docs"), "*.md")
                .Append(Path.Combine(Root, "README.md"))
                .Append(Path.Combine(Root, "scripts/verification.md"));
            foreach (var file in files)
            {
                foreach (Match match in Regex.Matches(File.ReadAllText(file), @"\[[^\]]*\]\(([^)]+)\)"))
                {
                    var target = match.Groups[1].Value.Trim('<', '>');
                    if (target.StartsWith("http") || target.StartsWith("#"))
                    {
                        continue;
                    }

                    target = target.Split('#')[0];
                    // Desktop file links may include a verified source-line suffix.
                    target = Regex.Replace(target, @"(?<=\.[a-zA-Z0-9]+):\d+$", "");
                    var path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file)!, Uri.UnescapeDataString(target)));
                    Assert.True(File.Exists(path) || Directory.Exists(path), file + " has broken link " + target);
                }
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
        // EndpointCompatibilityTests verifies the frozen routes and policies through
        // actual MVC endpoint metadata, independently of controller filenames.
    }
}
