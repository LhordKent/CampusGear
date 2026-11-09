using System.Reflection;
using CampusGear.Data;
using CampusGear.Models;
using CampusGear.Services;
using Microsoft.AspNetCore.Mvc;
namespace CampusGear.IntegrationTests;
public sealed class ArchitectureBoundaryTests
{
    private static readonly Type[] Entities = [typeof(ApplicationUser),typeof(BorrowerProfile),typeof(EquipmentItem),
 typeof(EquipmentCategory),typeof(Reservation),typeof(Loan),typeof(MaintenanceCase),typeof(AuditEvent),typeof(EmailChallenge)];
    private static string SolutionRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CampusGear.sln"))) dir = dir.Parent;
            return dir?.FullName ?? throw new InvalidOperationException("Cannot find source checkout for architecture checks.");
        }
    }
    [Fact]
    public void Live_web_controllers_views_and_helpers_have_no_database_or_identity_manager_access()
    {
        foreach (var folder in new[] { "Controllers", "Views", "Models", "ViewComponents" })
            foreach (var file in Directory.EnumerateFiles(Path.Combine(SolutionRoot, "CampusGear.WebApp", folder), "*", SearchOption.AllDirectories)
            .Where(x => x.EndsWith(".cs") || x.EndsWith(".cshtml")))
            {
                var source = File.ReadAllText(file);
                foreach (var token in new[] { "ApplicationDbContext", "EntityFrameworkCore", "SqlClient", "DbSet<", "IQueryable<", "UserManager<", "SignInManager<", ".Data.Repositories", ".Data.Interfaces" })
                    Assert.False(source.Contains(token), file + " references forbidden dependency " + token);
            }
    }
    [Fact]
    public void Controllers_take_only_application_and_transport_dependencies()
    {
        var controllers = typeof(Program).Assembly.GetTypes().Where(t => typeof(Controller).IsAssignableFrom(t));
        foreach (var type in controllers)
            foreach (var parameter in type.GetConstructors().SelectMany(c => c.GetParameters()))
            {
                var dependency = parameter.ParameterType;
                Assert.False(dependency.Assembly == typeof(ApplicationDbContext).Assembly, type.Name + " directly takes " + dependency.Name);
                Assert.False(dependency.Name.StartsWith("UserManager") || dependency.Name.StartsWith("SignInManager"), type.Name + " takes an Identity manager");
            }
    }
    [Fact]
    public void Business_services_contain_no_provider_or_mvc_apis()
    {
        foreach (var file in Directory.EnumerateFiles(Path.Combine(SolutionRoot, "CampusGear.Services"), "*.cs", SearchOption.AllDirectories)
        .Where(x => !x.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)))
            foreach (var token in new[] { "EntityFrameworkCore", "SqlClient", "ApplicationDbContext", "DbSet<", "IQueryable<", "IActionResult", "ControllerBase", "ModelState", "TempData" })
                Assert.False(File.ReadAllText(file).Contains(token), file + " references " + token);
    }
    [Fact]
    public void Data_contracts_never_expose_query_providers_or_ef_transactions()
    {
        var contracts = typeof(ApplicationDbContext).Assembly.GetTypes().Where(t => t.IsInterface && t.Namespace == "CampusGear.Data.Interfaces");
        foreach (var contract in contracts)
            foreach (var method in contract.GetMethods())
            {
                AssertBoundary(method.ReturnType, contract.Name + "." + method.Name, new());
                foreach (var parameter in method.GetParameters()) AssertBoundary(parameter.ParameterType, contract.Name + "." + method.Name, new());
            }
    }
    private static void AssertBoundary(Type type, string owner, HashSet<Type> visited)
    {
        if (!visited.Add(type)) return;
        Assert.False(type.Namespace?.StartsWith("Microsoft.EntityFrameworkCore") == true, owner + " leaks " + type);
        Assert.False(type == typeof(IQueryable) || type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IQueryable<>), owner + " leaks IQueryable");
        foreach (var argument in type.GetGenericArguments()) AssertBoundary(argument, owner, visited);
    }
    [Fact]
    public void Presentation_models_never_contain_mutable_database_entities()
    {
        var models = typeof(ICategoryService).Assembly.GetTypes().Where(t => t.IsPublic && (t.Namespace == "CampusGear.Controllers" || t.Namespace == "CampusGear.Services.ServiceModels"))
        .Concat(typeof(Program).Assembly.GetTypes().Where(t => t.IsPublic && t.Namespace == "CampusGear.Models"));
        foreach (var model in models)
            foreach (var property in model.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                AssertDisplay(property.PropertyType, model.Name + "." + property.Name, new());
    }
    private static void AssertDisplay(Type type, string owner, HashSet<Type> visited)
    {
        if (!visited.Add(type)) return;
        Assert.DoesNotContain(type, Entities);
        if (type.IsArray) AssertDisplay(type.GetElementType()!, owner, visited);
        foreach (var arg in type.GetGenericArguments()) AssertDisplay(arg, owner, visited);
        if (type.Assembly == typeof(ICategoryService).Assembly || type.Namespace == "CampusGear.Data.QueryModels")
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)) AssertDisplay(prop.PropertyType, owner, visited);
    }
}
