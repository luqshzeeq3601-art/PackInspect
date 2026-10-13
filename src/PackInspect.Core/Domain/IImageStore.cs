using PackInspect.Core.Devices;

namespace PackInspect.Core.Domain;

public interface IImageStore
{
    /// <summary>Saves the image of one unit and returns its path relative to the image folder.</summary>
    Task<string> SaveAsync(int lotId, int sequence, CapturedImage image, CancellationToken cancellationToken);

    /// <summary>The full file path of an image that <see cref="SaveAsync"/> stored.</summary>
    string GetFilePath(string storedPath);
}
