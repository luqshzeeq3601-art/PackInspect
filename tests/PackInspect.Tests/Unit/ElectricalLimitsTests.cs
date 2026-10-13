using PackInspect.Core.Domain;

namespace PackInspect.Tests.Unit;

public class ElectricalLimitsTests
{
    private static readonly ElectricalLimits Limits = new(Low: 95, High: 105);

    [Theory]
    [InlineData(100.0)]
    [InlineData(95.0)]
    [InlineData(105.0)]
    [InlineData(95.0001)]
    [InlineData(104.9999)]
    public void Value_inside_or_on_a_limit_passes(double value) =>
        Assert.Equal(Verdict.Pass, Limits.Evaluate(value));

    [Theory]
    [InlineData(94.9999)]
    [InlineData(105.0001)]
    [InlineData(0.0)]
    [InlineData(-100.0)]
    [InlineData(9.9E37)]
    public void Value_outside_the_limits_fails(double value) =>
        Assert.Equal(Verdict.Fail, Limits.Evaluate(value));

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Reading_that_is_not_a_real_number_fails(double value) =>
        Assert.Equal(Verdict.Fail, Limits.Evaluate(value));

    [Fact]
    public void Limits_that_are_equal_pass_only_that_exact_value()
    {
        var exact = new ElectricalLimits(3.3, 3.3);

        Assert.Equal(Verdict.Pass, exact.Evaluate(3.3));
        Assert.Equal(Verdict.Fail, exact.Evaluate(3.3001));
    }
}
