using System.Net.Sockets;
using PackInspect.Simulator.Net;
using PackInspect.Simulator.Units;

namespace PackInspect.Simulator.Imaging;

/// <summary>
/// A simulated camera. The command <c>CAPTURE</c> is answered with a header
/// line <c>IMAGE width height</c> followed by width x height grayscale bytes.
/// </summary>
internal sealed class CameraServer(int port, StationModel station) : TcpTextServer(port)
{
    protected override async Task HandleLineAsync(string line, NetworkStream stream, CancellationToken cancellationToken)
    {
        if (!line.Equals("CAPTURE", StringComparison.OrdinalIgnoreCase))
        {
            await WriteLineAsync(stream, "ERROR unknown command", cancellationToken).ConfigureAwait(false);
            return;
        }

        var image = station.CaptureImage();
        await WriteLineAsync(stream, $"IMAGE {image.Width} {image.Height}", cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(image.Pixels, cancellationToken).ConfigureAwait(false);
    }
}
