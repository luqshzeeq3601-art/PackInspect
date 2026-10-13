using PackInspect.Devices.Barcode;
using PackInspect.LegacyDriver;

namespace PackInspect.Tests.Unit;

public class LegacyBarcodeReaderAdapterTests
{
    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Adapter_reads_and_validates_barcode_from_legacy_driver()
    {
        var scanner = new LegacyBarcodeReader();
        var adapter = new LegacyBarcodeReaderAdapter(scanner, "COM1");

        string barcode = await adapter.ReadBarcodeAsync(42, TestToken);

        Assert.Equal("U000042", barcode);
    }

    [Theory]
    [InlineData("U000001")]
    [InlineData("LOT-A_UNIT-109")]
    [InlineData("ABC123XYZ")]
    [InlineData("QFN-5X5-001")]
    public void Allowed_barcode_formats_pass_validation(string barcode)
    {
        string validated = LegacyBarcodeReaderAdapter.ValidateBarcode(barcode);
        Assert.Equal(barcode, validated);
    }

    [Theory]
    [InlineData("U00001; DROP TABLE")]
    [InlineData("UNIT<script>")]
    [InlineData("UNIT 1234")]
    [InlineData("U#001!")]
    [InlineData("U$001")]
    public void Disallowed_characters_in_barcodes_are_rejected(string barcode)
    {
        Assert.Throws<FormatException>(() => LegacyBarcodeReaderAdapter.ValidateBarcode(barcode));
    }

    [Fact]
    public void Overly_long_barcode_exceeding_32_characters_is_rejected()
    {
        string tooLong = new('A', 33);
        Assert.Throws<FormatException>(() => LegacyBarcodeReaderAdapter.ValidateBarcode(tooLong));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_or_whitespace_barcode_is_rejected(string? barcode)
    {
        Assert.Throws<FormatException>(() => LegacyBarcodeReaderAdapter.ValidateBarcode(barcode));
    }
}
