using PackInspect.Devices.Scpi;

namespace PackInspect.Tests.Unit;

public class ScpiReplyTests
{
    [Theory]
    [InlineData("+9.98700000E+01", 99.87)]
    [InlineData("-1.25000000E-03", -0.00125)]
    [InlineData("100", 100.0)]
    [InlineData("  3.3  ", 3.3)]
    [InlineData("+9.90000000E+37", 9.9E37)]
    public void Numeric_reply_is_parsed(string reply, double expected) =>
        Assert.Equal(expected, ScpiReply.ParseNumber(reply), precision: 9);

    [Theory]
    [InlineData("")]
    [InlineData("ERROR")]
    [InlineData("1,5")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("0,\"No error\"")]
    public void Reply_that_is_not_a_finite_number_is_rejected(string reply) =>
        Assert.Throws<FormatException>(() => ScpiReply.ParseNumber(reply));

    [Fact]
    public void Decimal_point_is_always_a_dot_whatever_the_pc_culture_is()
    {
        var original = Thread.CurrentThread.CurrentCulture;
        try
        {
            // German uses a decimal comma; an instrument never does.
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            Assert.Equal(99.87, ScpiReply.ParseNumber("+9.98700000E+01"), precision: 9);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = original;
        }
    }
}
