namespace CampusGear.Services;

/// <summary>Stores equipment photos as static files and returns their web paths.</summary>
public interface IEquipmentImageStore
{
    /// <summary>The largest accepted upload, in bytes.</summary>
    long MaxBytes { get; }

    /// <summary>
    /// Validates the image by its content (JPEG, PNG, WebP or GIF) and saves it under a server-chosen name.
    /// Returns the web path to store in <c>EquipmentItem.ImagePath</c>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The file is empty, too large, or not a supported image.</exception>
    Task<string> SaveAsync(Guid equipmentItemId, Stream content, long length, CancellationToken cancellationToken = default);

    /// <summary>Deletes a file previously returned by <see cref="SaveAsync"/>. Unknown paths are ignored.</summary>
    void Delete(string? webPath);
}
