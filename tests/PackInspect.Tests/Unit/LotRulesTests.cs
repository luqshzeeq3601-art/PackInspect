using PackInspect.Core.Domain;

namespace PackInspect.Tests.Unit;

public class LotRulesTests
{
    [Theory]
    [InlineData("LOT-0001", 1)]
    [InlineData("lot_2026_10", 10000)]
    [InlineData("A", 20)]
    public void Valid_input_is_accepted(string lotNumber, int quantity) =>
        Assert.Null(LotRules.Validate(lotNumber, quantity));

    [Theory]
    [InlineData(null, 20)]
    [InlineData("", 20)]
    [InlineData("   ", 20)]
    [InlineData("LOT 1", 20)]
    [InlineData("../LOT", 20)]
    [InlineData("LOT;DROP", 20)]
    [InlineData("LOT-0001", 0)]
    [InlineData("LOT-0001", -5)]
    [InlineData("LOT-0001", 10001)]
    public void Invalid_input_is_rejected_with_a_message(string? lotNumber, int quantity) =>
        Assert.False(string.IsNullOrEmpty(LotRules.Validate(lotNumber, quantity)));

    [Fact]
    public void Lot_number_longer_than_the_limit_is_rejected() =>
        Assert.NotNull(LotRules.Validate(new string('A', LotRules.MaxLotNumberLength + 1), 20));
}
