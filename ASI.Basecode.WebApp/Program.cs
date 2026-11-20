using ASI.Basecode.Data;
using ASI.Basecode.WebApp;

var appBuilder = WebApplication.CreateBuilder(args);

var configurer = new StartupConfigurer(appBuilder.Configuration, appBuilder.Environment, args);
configurer.ConfigureServices(appBuilder.Services);

var app = appBuilder.Build();

configurer.ConfigureApp(app);

app.MapControllers();

// Applies migrations and creates the roles (and, when configured, the first administrator).
await DbInitializer.InitializeAsync(app.Services, app.Environment.IsDevelopment());

// Demo Mode (DemoMode:Enabled, on in appsettings.Development.json) adds sample records for the demo accounts. It is
// refused outside Development so a real deployment can never receive demo data; see ASI.Basecode.Data/DemoData.cs.
if (app.Environment.IsDevelopment() && args.Contains("--remove-demo-data", StringComparer.OrdinalIgnoreCase))
{
    await using var scope = app.Services.CreateAsyncScope();
    app.Logger.LogInformation(
        "Demo Mode: {Result}",
        await DemoData.RemoveAsync(scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>())
    );
    return;
}

if (app.Configuration.GetValue<bool>("DemoMode:Enabled"))
{
    if (!app.Environment.IsDevelopment())
    {
        app.Logger.LogWarning("DemoMode:Enabled is ignored outside the Development environment; no demo data was added.");
    }
    else
    {
        await using var scope = app.Services.CreateAsyncScope();
        app.Logger.LogInformation(
            "Demo Mode: {Result}",
            await DemoData.SeedAsync(
                scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>(),
                TimeProvider.System.GetUtcNow().UtcDateTime
            )
        );
    }
}

await DbInitializer.InitializeAssetCodeSeriesAsync(app.Services);

if (args.Contains("--initialize-only", StringComparer.OrdinalIgnoreCase))
{
    return;
}

// Run application
app.Run();

public partial class Program;
