using System.Net;
using System.Text.RegularExpressions;
using CampusGear.Data;
using CampusGear.Models;
using CampusGear.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CampusGear.IntegrationTests;

[CollectionDefinition("Web app permissions", DisableParallelization = true)]
public sealed class WebAppCollection : ICollectionFixture<WebAppFixture> { }

/// <summary>
/// Runs the real web app (routing, cookie auth, role checks, antiforgery, views) in memory against a throwaway SQL
/// Server database, and signs one account per role in through the real login form.
/// </summary>
public sealed partial class WebAppFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string Password = "Permission-Test!42";
    private const string DatabasePrefix = "CampusGear_Integration_";
    private readonly string _databaseName = DatabasePrefix + Guid.NewGuid().ToString("N");
    private readonly string _connectionString;

    public WebAppFixture()
    {
        var baseConnection = Environment.GetEnvironmentVariable("CAMPUSGEAR_TEST_SQLSERVER")
            ?? "Server=.\\SQLEXPRESS;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True";
        _connectionString = new SqlConnectionStringBuilder(baseConnection) { InitialCatalog = _databaseName }.ConnectionString;
    }

    public TestAccount BorrowerA { get; private set; } = null!;
    public TestAccount BorrowerB { get; private set; } = null!;
    public TestAccount Custodian { get; private set; } = null!;
    public TestAccount Administrator { get; private set; } = null!;
    public Guid CategoryId { get; private set; }

    /// <summary>Where each role landed after signing in.</summary>
    public Dictionary<string, string> LandingPaths { get; } = [];

    public HttpClient Client(string role) => role switch
    {
        "Borrower" => BorrowerA.Client!,
        "Custodian" => Custodian.Client!,
        "Administrator" => Administrator.Client!,
        _ => NewClient()
    };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Email:Provider", "Development");
        builder.UseSetting("DemoMode:Enabled", "false");
        builder.UseSetting("ConnectionStrings:CampusGear", _connectionString);
        // Replace the database registration outright so no configuration source can point tests at a real database.
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(_connectionString));
        });
    }

    public async Task InitializeAsync()
    {
        _ = Server; // Starts the app; Development startup migrates the database and seeds three sample items.
        await using (var scope = Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            if (db.Database.GetDbConnection().Database != _databaseName)
                throw new InvalidOperationException("Refusing to run permission tests outside the throwaway database.");
            CategoryId = await db.EquipmentCategories.Select(x => x.Id).FirstAsync();
        }
        BorrowerA = await NewAccountAsync("Borrower");
        BorrowerB = await NewAccountAsync("Borrower");
        Custodian = await NewAccountAsync("Custodian");
        Administrator = await NewAccountAsync("Administrator");
        foreach (var account in new[] { BorrowerA, BorrowerB, Custodian, Administrator })
        {
            var (client, landing) = await SignInAsync(account.Email);
            account.Client = client;
            LandingPaths.TryAdd(account.Role, landing);
        }
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        var builder = new SqlConnectionStringBuilder(_connectionString);
        if (builder.InitialCatalog != _databaseName || !_databaseName.StartsWith(DatabasePrefix, StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing to delete an unexpected test database.");
        await using (var scope = Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureDeletedAsync();
        await base.DisposeAsync();
    }

    public HttpClient NewClient()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
        return client;
    }

    /// <summary>Creates an approved, verified account; borrowers also get an eligible profile.</summary>
    public async Task<TestAccount> NewAccountAsync(string role, bool active = true)
    {
        await using var scope = Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}@permission.test";
        var user = new ApplicationUser
        {
            UserName = email, Email = email, EmailConfirmed = true, FirstName = "Test", LastName = role,
            ApprovalStatus = AccountApprovalStatus.Approved, IsActive = active
        };
        Assert.True((await users.CreateAsync(user, Password)).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
        Guid? profileId = null;
        if (role == "Borrower")
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var profile = new BorrowerProfile { UserId = user.Id, IsEligible = true, StudentNumber = Guid.NewGuid().ToString("N")[..12] };
            db.BorrowerProfiles.Add(profile);
            await db.SaveChangesAsync();
            profileId = profile.Id;
        }
        return new TestAccount(role, user.Id, email, profileId);
    }

    /// <summary>Signs in through the login form, completing the administrator email code when asked for it.</summary>
    public async Task<(HttpClient Client, string Landing)> SignInAsync(string email)
    {
        var client = NewClient();
        var response = await PostFormAsync(client, "/auth/login", "/auth/login", ("Email", email), ("Password", Password));
        if (response.Headers.Location?.OriginalString == "/auth/two-factor")
        {
            var sender = Services.GetRequiredService<DevelopmentEmailSender>();
            var code = CodePattern().Match(sender.GetRecent().First(x => x.ToEmail == email).Body).Value;
            response = await PostFormAsync(client, "/auth/two-factor", "/auth/two-factor", ("Code", code));
        }
        return (client, response.StatusCode == HttpStatusCode.Redirect ? response.Headers.Location!.OriginalString : string.Empty);
    }

    /// <summary>Posts a form with an antiforgery token taken from <paramref name="tokenPage"/>, as a browser would.</summary>
    public static async Task<HttpResponseMessage> PostFormAsync(HttpClient client, string path, string tokenPage,
        params (string Name, string Value)[] fields)
    {
        var page = await client.GetStringAsync(tokenPage);
        var token = TokenPattern().Match(page).Groups[1].Value;
        var form = fields.Select(x => new KeyValuePair<string, string>(x.Name, x.Value))
            .Append(new("__RequestVerificationToken", token));
        return await client.PostAsync(path, new FormUrlEncodedContent(form));
    }

    public async Task<T> DbAsync<T>(Func<ApplicationDbContext, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    /// <summary>A single-unit item with a unique name, so it forms its own catalog product.</summary>
    public Task<Guid> NewItemAsync(Guid? categoryId = null) => DbAsync(async db =>
    {
        var item = new EquipmentItem
        {
            CategoryId = categoryId ?? CategoryId, AssetCode = "PT-" + Guid.NewGuid().ToString("N")[..10],
            Name = "Permission test unit " + Guid.NewGuid().ToString("N")[..6], Condition = EquipmentCondition.Good
        };
        db.EquipmentItems.Add(item);
        await db.SaveChangesAsync();
        return item.Id;
    });

    /// <summary>Inserts a reservation directly, for states a borrower cannot create through the form (e.g. one already under way).</summary>
    public Task<Guid> NewReservationAsync(Guid profileId, Guid itemId, ReservationStatus status, DateTime startUtc, DateTime endUtc) =>
        DbAsync(async db =>
        {
            var reservation = new Reservation
            {
                BorrowerProfileId = profileId, EquipmentItemId = itemId, Status = status, StartAtUtc = startUtc,
                EndAtUtc = endUtc, Purpose = "Permission test booking",
                HoldExpiresAtUtc = status == ReservationStatus.Pending ? startUtc : null
            };
            db.Reservations.Add(reservation);
            await db.SaveChangesAsync();
            return reservation.Id;
        });

    [GeneratedRegex(@"\b\d{6}\b")]
    private static partial Regex CodePattern();
    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"")]
    private static partial Regex TokenPattern();
}

public sealed class TestAccount(string role, string userId, string email, Guid? profileId)
{
    public string Role { get; } = role;
    public string UserId { get; } = userId;
    public string Email { get; } = email;
    public Guid? ProfileId { get; } = profileId;
    public HttpClient? Client { get; set; }
}
