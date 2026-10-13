using PackInspect.LegacyDriver;

namespace PackInspect.Tests.Unit;

public class LegacyBarcodeReaderTests
{
    [Fact]
    public void Scanner_connects_and_disconnects_properly()
    {
        var scanner = new LegacyBarcodeReader();
        Assert.False(scanner.IsConnected);

        scanner.Connect("COM3");
        Assert.True(scanner.IsConnected);

        scanner.Disconnect();
        Assert.False(scanner.IsConnected);
    }

    [Fact]
    public void Read_barcode_returns_formatted_industrial_code()
    {
        var scanner = new LegacyBarcodeReader();
        scanner.Connect("COM3");

        string barcode = scanner.ReadBarcode(17);

        Assert.Equal("U000017", barcode);
    }

    [Fact]
    public void Sequential_scans_increment_internal_counter()
    {
        var scanner = new LegacyBarcodeReader();
        scanner.Connect("COM3");

        Assert.Equal("U000001", scanner.Scan());
        Assert.Equal("U000002", scanner.Scan());
        Assert.Equal("U000003", scanner.Scan());
    }

    [Fact]
    public void Read_when_disconnected_throws_invalid_operation()
    {
        var scanner = new LegacyBarcodeReader();

        Assert.Throws<InvalidOperationException>(() => scanner.ReadBarcode(1));
    }

    [Fact]
    public void Negative_sequence_number_throws_argument_out_of_range()
    {
        var scanner = new LegacyBarcodeReader();
        scanner.Connect("COM3");

        Assert.Throws<ArgumentOutOfRangeException>(() => scanner.ReadBarcode(-1));
    }
}
