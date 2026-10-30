using Microsoft.AspNetCore.Hosting;

namespace CampusGear.Services;

/// <summary>
/// Saves equipment photos to wwwroot/uploads/equipment. The file type is decided from the
/// file's leading bytes, never from the uploaded name or content type, and the stored name is generated.
/// </summary>
public sealed class EquipmentImageStore(IWebHostEnvironment environment) : IEquipmentImageStore
{
    private const string WebFolder = "/uploads/equipment/";

    public long MaxBytes => 5 * 1024 * 1024;

    public async Task<string> SaveAsync(Guid equipmentItemId, Stream content, long length,
        CancellationToken cancellationToken = default)
    {
        if (length <= 0)
            throw new InvalidOperationException("Choose an image file to upload.");
        if (length > MaxBytes)
            throw new InvalidOperationException("Use an image of 5 MB or smaller.");

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length > MaxBytes)
            throw new InvalidOperationException("Use an image of 5 MB or smaller.");
        var extension = DetectExtension(buffer.GetBuffer().AsSpan(0, (int)buffer.Length))
            ?? throw new InvalidOperationException("Upload a JPEG, PNG, WebP or GIF image.");

        var folder = PhysicalFolder();
        Directory.CreateDirectory(folder);
        var fileName = $"{equipmentItemId:N}-{Guid.NewGuid():N}{extension}";
        buffer.Position = 0;
        await using (var file = new FileStream(Path.Combine(folder, fileName), FileMode.CreateNew, FileAccess.Write))
            await buffer.CopyToAsync(file, cancellationToken);
        return WebFolder + fileName;
    }

    public void Delete(string? webPath)
    {
        if (string.IsNullOrEmpty(webPath) || !webPath.StartsWith(WebFolder, StringComparison.Ordinal))
            return;
        var fileName = webPath[WebFolder.Length..];
        // Only bare generated names are deleted, so a stored path can never reach outside the folder.
        if (fileName.Length == 0 || fileName != Path.GetFileName(fileName))
            return;
        try { File.Delete(Path.Combine(PhysicalFolder(), fileName)); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    // WebRootPath is null when the app starts without a wwwroot folder; fall back to where it belongs.
    private string PhysicalFolder() => Path.Combine(
        environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"), "uploads", "equipment");

    private static string? DetectExtension(ReadOnlySpan<byte> data)
    {
        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
            return ".jpg";
        if (data.Length >= 8 && data[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
            return ".png";
        if (data.Length >= 12 && data[..4].SequenceEqual("RIFF"u8) && data[8..12].SequenceEqual("WEBP"u8))
            return ".webp";
        if (data.Length >= 6 && (data[..6].SequenceEqual("GIF87a"u8) || data[..6].SequenceEqual("GIF89a"u8)))
            return ".gif";
        return null;
    }
}
