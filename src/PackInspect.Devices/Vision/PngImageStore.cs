using System.Globalization;
using OpenCvSharp;
using PackInspect.Core.Devices;
using PackInspect.Core.Domain;

namespace PackInspect.Devices.Vision;

/// <summary>Saves every unit image as a PNG, so a result can be traced back to what the camera saw.</summary>
public sealed class PngImageStore(ImageStoreOptions options) : IImageStore
{
    public async Task<string> SaveAsync(int lotId, int sequence, CapturedImage image, CancellationToken cancellationToken)
    {
        // The path is built only from numbers the app generated, never from text
        // a user typed (such as the lot number), so it cannot point outside the folder.
        string folder = string.Create(CultureInfo.InvariantCulture, $"lot-{lotId:000000}");
        string file = string.Create(CultureInfo.InvariantCulture, $"unit-{sequence:0000}.png");

        using var mat = Mat.FromPixelData(image.Height, image.Width, MatType.CV_8UC1, image.Pixels.ToArray());
        if (!Cv2.ImEncode(".png", mat, out byte[] png))
        {
            throw new IOException("The image could not be encoded as PNG.");
        }

        Directory.CreateDirectory(Path.Combine(options.RootDirectory, folder));
        await File.WriteAllBytesAsync(Path.Combine(options.RootDirectory, folder, file), png, cancellationToken).ConfigureAwait(false);
        return $"{folder}/{file}";
    }

    public string GetFilePath(string storedPath) => Path.GetFullPath(Path.Combine(options.RootDirectory, storedPath));
}
