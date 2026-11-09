using CampusGear.Data;
using CampusGear.Data.Interfaces;
using CampusGear.Services;
using CampusGear.Services.ServiceModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CampusGear.IntegrationTests;

[Collection("Web app permissions")]
public sealed class InventoryCompensationTests(WebAppFixture app)
{
    [Fact]
    public async Task Replacing_then_removing_a_photo_commits_path_before_deleting_old_content()
    {
        var id = await app.NewItemAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var images = new RecordingImages();
        var service = Service(scope.ServiceProvider, images);
        var command = await Command(db, id);
        Assert.True((await service.SaveAsync(Actor(), command, Photo(), default)).Succeeded);
        var old = await db.EquipmentItems.Where(x => x.Id == id).Select(x => x.ImagePath).SingleAsync();
        Assert.Contains(old!, images.Files);
        command = await Command(db, id);
        Assert.True((await service.SaveAsync(Actor(), command, Photo(), default)).Succeeded);
        Assert.DoesNotContain(old!, images.Files);
        Assert.Single(images.Files);
        command = await Command(db, id);
        command.RemoveImage = true;
        Assert.True((await service.SaveAsync(Actor(), command, null, default)).Succeeded);
        Assert.Null(await db.EquipmentItems.Where(x => x.Id == id).Select(x => x.ImagePath).SingleAsync());
        Assert.Empty(images.Files);
    }

    [Fact]
    public async Task Duplicate_asset_failure_removes_new_photo_and_keeps_original_record()
    {
        var id = await app.NewItemAsync();
        var other = await app.NewItemAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var images = new RecordingImages();
        var service = Service(scope.ServiceProvider, images);
        var command = await Command(db, id);
        var originalAsset = command.AssetCode;
        command.AssetCode = await db.EquipmentItems.Where(x => x.Id == other).Select(x => x.AssetCode).SingleAsync();
        var result = await service.SaveAsync(Actor(), command, Photo(), default);
        Assert.False(result.Succeeded);
        Assert.Empty(images.Files);
        var item = await db.EquipmentItems.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.Null(item.ImagePath);
        Assert.Equal(originalAsset, item.AssetCode);
        Assert.False(await db.AuditEvents.AnyAsync(x => x.EntityId == id.ToString()));
    }

    private RequestContext Actor() => new(app.Administrator.UserId, "127.0.0.1", "inventory-compensation-test");
    private static UploadContent Photo() => new(new MemoryStream([1, 2, 3]), 3);
    private static InventoryService Service(IServiceProvider services, IEquipmentImageStore images) => new(
        services.GetRequiredService<IEquipmentRepository>(), services.GetRequiredService<ICategoryRepository>(),
        services.GetRequiredService<IMaintenanceRepository>(), services.GetRequiredService<IAuditRepository>(),
        services.GetRequiredService<IUnitOfWork>(), services.GetRequiredService<IWorkflowLockManager>(), TimeProvider.System, images);
    private static async Task<EquipmentCommand> Command(ApplicationDbContext db, Guid id)
    {
        var item = await db.EquipmentItems.AsNoTracking().SingleAsync(x => x.Id == id);
        return new()
        {
            Id = id,
            CategoryId = item.CategoryId,
            AssetCode = item.AssetCode,
            Name = item.Name,
            Condition = item.Condition,
            IsActive = item.IsActive,
            RowVersion = Convert.ToBase64String(item.RowVersion)
        };
    }
    private sealed class RecordingImages : IEquipmentImageStore
    {
        public long MaxBytes => 5 * 1024 * 1024;
        public HashSet<string> Files { get; } = [];
        public Task<string> SaveAsync(Guid id, Stream stream, long length, CancellationToken ct = default)
        {
            var name = $"/uploads/equipment/{id:N}-{Guid.NewGuid():N}.png";
            Files.Add(name);
            return Task.FromResult(name);
        }
        public void Delete(string? path) { if (path is not null) Files.Remove(path); }
    }
}
