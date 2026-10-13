namespace PackInspect.Core.Devices;

/// <summary>
/// An 8-bit grayscale image, one byte per pixel, rows top to bottom.
/// Core keeps images as plain bytes so it does not depend on OpenCV.
/// </summary>
public sealed record CapturedImage(int Width, int Height, ReadOnlyMemory<byte> Pixels);
