using System.Data.Common;
using CampusGear.Data;
using CampusGear.Models;
using CampusGear.Services.Reservations;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CampusGear.IntegrationTests;

[CollectionDefinition("SQL reservation lifecycle", DisableParallelization = true)]
public sealed class SqlReservationCollection : ICollectionFixture<SqlReservationFixture> { }

public sealed class SqlReservationFixture : IAsyncLifetime
{
    private const string DatabasePrefix = "CampusGear_Integration_";
    private readonly string _databaseName = DatabasePrefix + Guid.NewGuid().ToString("N");
    private ServiceProvider _provider = null!;
    private string _connectionString = string.Empty;
    private Guid _categoryId;

    public TestClock Clock { get; } = new();
    public BorrowerLockObserver LockObserver { get; } = new();
    public string StaffUserId { get; private set; } = string.Empty;
    public string AdminUserId { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        var baseConnection = Environment.GetEnvironmentVariable("CAMPUSGEAR_TEST_SQLSERVER")
            ?? "Server=.\\SQLEXPRESS;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True";
        var builder = new SqlConnectionStringBuilder(baseConnection) { InitialCatalog = _databaseName };
        _connectionString = builder.ConnectionString;

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options => options
            .UseSqlServer(_connectionString).AddInterceptors(LockObserver));
        services.AddIdentityCore<ApplicationUser>(options => options.Password.RequiredLength = 8)
            .AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
        services.AddSingleton<TimeProvider>(Clock);
        services.AddScoped<IReservationService, ReservationService>();
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        await using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { "Borrower", "Custodian", "Administrator" })
            Assert.True((await roles.CreateAsync(new IdentityRole(role))).Succeeded);

        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        StaffUserId = await AddUserAsync(users, "Custodian");
        AdminUserId = await AddUserAsync(users, "Administrator");
        var category = new EquipmentCategory { Name = "Integration test equipment" };
        db.EquipmentCategories.Add(category);
        await db.SaveChangesAsync();
        _categoryId = category.Id;
    }

    public async Task DisposeAsync()
    {
        if (_provider is null) return;
        // The generated name is the only deletion target. Never use a configured live database name.
        var builder = new SqlConnectionStringBuilder(_connectionString);
        if (builder.InitialCatalog != _databaseName || !_databaseName.StartsWith(DatabasePrefix, StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing to delete an unexpected test database.");
        await using (var scope = CreateScope())
            await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureDeletedAsync();
        await _provider.DisposeAsync();
    }

    public AsyncServiceScope CreateScope() => _provider.CreateAsyncScope();

    public async Task<TestBorrower> NewBorrowerAsync()
    {
        await using var scope = CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var userId = await AddUserAsync(users, "Borrower");
        var profile = new BorrowerProfile { UserId = userId, IsEligible = true };
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.BorrowerProfiles.Add(profile);
        await db.SaveChangesAsync();
        return new TestBorrower(userId, profile.Id);
    }

    public async Task<Guid> NewItemAsync()
    {
        await using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var item = new EquipmentItem
        {
            CategoryId = _categoryId,
            AssetCode = "TEST-" + Guid.NewGuid().ToString("N"),
            Name = "Integration test projector",
            Condition = EquipmentCondition.Good
        };
        db.EquipmentItems.Add(item);
        await db.SaveChangesAsync();
        return item.Id;
    }

    public async Task<T> ServiceAsync<T>(Func<IReservationService, Task<T>> action)
    {
        await using var scope = CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<IReservationService>());
    }

    public async Task<T> DbAsync<T>(Func<ApplicationDbContext, Task<T>> action)
    {
        await using var scope = CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    private static async Task<string> AddUserAsync(UserManager<ApplicationUser> users, string role)
    {
        var email = $"{Guid.NewGuid():N}@integration.test";
        var user = new ApplicationUser
        {
            UserName = email, Email = email, FullName = "Integration test " + role,
            EmailConfirmed = true, IsActive = true
        };
        var result = await users.CreateAsync(user, "Integration-Pass!42");
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(x => x.Description)));
        result = await users.AddToRoleAsync(user, role);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(x => x.Description)));
        return user.Id;
    }
}

public sealed record TestBorrower(string UserId, Guid ProfileId);

public sealed class TestClock : TimeProvider
{
    private long _utcTicks = new DateTime(2030, 1, 10, 0, 0, 0, DateTimeKind.Utc).Ticks;
    public override DateTimeOffset GetUtcNow() => new(new DateTime(Interlocked.Read(ref _utcTicks), DateTimeKind.Utc));
    public void Set(DateTime utc) => Interlocked.Exchange(ref _utcTicks, utc.Ticks);
}

public sealed class BorrowerLockObserver : DbCommandInterceptor
{
    private readonly object _gate = new();
    private string? _resource;
    private TaskCompletionSource<bool>? _attempt;

    public Task Arm(string resource)
    {
        lock (_gate)
        {
            _resource = resource;
            _attempt = new(TaskCreationOptions.RunContinuationsAsynchronously);
            return _attempt.Task;
        }
    }

    public void Disarm()
    {
        lock (_gate) { _resource = null; _attempt = null; }
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_resource is not null && command.CommandText.Contains("sp_getapplock", StringComparison.OrdinalIgnoreCase) &&
                command.Parameters.Cast<DbParameter>().Any(x => Equals(x.Value, _resource)))
                _attempt?.TrySetResult(true);
        }
        return ValueTask.FromResult(result);
    }
}
