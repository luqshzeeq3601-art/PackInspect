using System.Globalization;
using PackInspect.Core.Devices;
using PackInspect.Devices.Net;

namespace PackInspect.Devices.Camera;

/// <summary>
/// A camera reached over TCP with the simple protocol in docs/Data.md: send
/// <c>CAPTURE</c>, receive <c>IMAGE width height</c> and then the pixels.
/// A real camera would be a different class behind the same interface.
/// </summary>
public sealed class TcpCamera(CameraOptions options) : ICamera, IDisposable
{
    private const int MaxHeaderLength = 64;

    // Refuse absurd sizes before allocating memory for them.
    private const int MaxDimension = 4096;

    private readonly TcpTextClient _client = new(options.Host, options.Port, options.Timeout);

    public Task<CapturedImage> CaptureAsync(CancellationToken cancellationToken) =>
        _client.ExchangeAsync(
            async (stream, token) =>
            {
                await TcpTextClient.WriteLineAsync(stream, "CAPTURE", token).ConfigureAwait(false);
                string header = await TcpTextClient.ReadLineAsync(stream, MaxHeaderLength, token).ConfigureAwait(false);
                var (width, height) = ParseHeader(header);

                byte[] pixels = new byte[width * height];
                await stream.ReadExactlyAsync(pixels, token).ConfigureAwait(false);
                return new CapturedImage(width, height, pixels);
            },
            cancellationToken);

    public void Dispose() => _client.Dispose();

    private static (int Width, int Height) ParseHeader(string header)
    {
        string[] parts = header.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts is ["IMAGE", var widthText, var heightText]
            && int.TryParse(widthText, NumberStyles.None, CultureInfo.InvariantCulture, out int width)
            && int.TryParse(heightText, NumberStyles.None, CultureInfo.InvariantCulture, out int height)
            && width is > 0 and <= MaxDimension
            && height is > 0 and <= MaxDimension)
        {
            return (width, height);
        }

        throw new IOException($"Unexpected camera reply '{header}'.");
    }
}
