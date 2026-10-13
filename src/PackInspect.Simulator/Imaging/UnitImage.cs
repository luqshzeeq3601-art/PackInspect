namespace PackInspect.Simulator.Imaging;

/// <summary>An 8-bit grayscale image, one byte per pixel, rows top to bottom.</summary>
public sealed record UnitImage(int Width, int Height, byte[] Pixels);
