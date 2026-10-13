using System.Text.RegularExpressions;
using PackInspect.Core.Devices;
using PackInspect.LegacyDriver;

namespace PackInspect.Devices.Barcode;

/// <summary>
/// Adapts the VB.NET LegacyBarcodeReader driver to the C# IBarcodeReader contract.
/// Enforces input sanitisation and validation rules defined in docs/Security.md.
/// </summary>
public sealed partial class LegacyBarcodeReaderAdapter : IBarcodeReader
{
    private const int MaxBarcodeLength = 32;
    private readonly LegacyBarcodeReader _legacyScanner;

    [GeneratedRegex("^[A-Za-z0-9_\\-]+$", RegexOptions.Compiled)]
    private static partial Regex AllowedBarcodePattern();

    public LegacyBarcodeReaderAdapter(LegacyBarcodeReader? legacyScanner = null, string portName = "COM3")
    {
        _legacyScanner = legacyScanner ?? new LegacyBarcodeReader();
        if (!_legacyScanner.IsConnected)
        {
            _legacyScanner.Connect(portName);
        }
    }

    public Task<string> ReadBarcodeAsync(int sequence, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string rawBarcode = _legacyScanner.ReadBarcode(sequence);

        string validated = ValidateBarcode(rawBarcode);
        return Task.FromResult(validated);
    }

    public static string ValidateBarcode(string? barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode))
        {
            throw new FormatException("Barcode scan returned empty or whitespace payload.");
        }

        string trimmed = barcode.Trim();
        if (trimmed.Length > MaxBarcodeLength)
        {
            throw new FormatException($"Barcode exceeds maximum allowed length of {MaxBarcodeLength} characters.");
        }

        if (!AllowedBarcodePattern().IsMatch(trimmed))
        {
            throw new FormatException($"Barcode '{trimmed}' contains invalid characters. Only alphanumeric, hyphen and underscore are permitted.");
        }

        return trimmed;
    }
}
