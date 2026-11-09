using CampusGear.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
namespace CampusGear.IntegrationTests;
public sealed class EquipmentImageStoreTests
{
    [Fact]
    public async Task Invalid_or_oversized_upload_creates_no_file()
    {
        using var area = new TemporaryImages(); var store = new EquipmentImageStore(area);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveAsync(Guid.NewGuid(), new MemoryStream("not an image"u8.ToArray()), 12));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveAsync(Guid.NewGuid(), new MemoryStream(), store.MaxBytes + 1));
        Assert.Empty(Directory.GetFiles(area.WebRootPath, "*", SearchOption.AllDirectories));
    }
    [Fact]
    public async Task Generated_image_can_be_deleted_but_untrusted_paths_cannot_escape_upload_folder()
    {
        using var area = new TemporaryImages(); var store = new EquipmentImageStore(area);
        var marker = Path.Combine(area.WebRootPath, "keep.txt"); await File.WriteAllTextAsync(marker, "keep");
        var content = new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a };
        var path = await store.SaveAsync(Guid.NewGuid(), new MemoryStream(content), content.Length);
        Assert.StartsWith("/uploads/equipment/", path); Assert.EndsWith(".png", path);
        var file = Path.Combine(area.WebRootPath, path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(file));
        store.Delete("/uploads/equipment/../../keep.txt"); store.Delete(marker);
        Assert.True(File.Exists(marker)); Assert.True(File.Exists(file));
        store.Delete(path); Assert.False(File.Exists(file));
    }
    private sealed class TemporaryImages : IWebHostEnvironment, IDisposable
    {
        public TemporaryImages() { Directory.CreateDirectory(WebRootPath); }
        public string WebRootPath { get; set; } = Path.Combine(Path.GetTempPath(), "CampusGear_ImageTest_" + Guid.NewGuid().ToString("N"));
        public string ContentRootPath { get; set; } = "";
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "CampusGear";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public void Dispose()
        {
            var full = Path.GetFullPath(WebRootPath);
            var parent = Path.GetFullPath(Path.GetTempPath());
            if (!full.StartsWith(parent, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(full).StartsWith("CampusGear_ImageTest_"))
                throw new InvalidOperationException("Unexpected temporary upload path.");
            Directory.Delete(full, true);
        }
    }
}
