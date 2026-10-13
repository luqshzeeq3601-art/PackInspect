namespace PackInspect.Core.Devices;

/// <summary>
/// Device contract for reading 1D/2D unit barcodes at the station.
/// </summary>
public interface IBarcodeReader
{
    Task<string> ReadBarcodeAsync(int sequence, CancellationToken cancellationToken);
}
