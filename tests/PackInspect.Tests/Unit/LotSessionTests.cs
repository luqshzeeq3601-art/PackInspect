using PackInspect.Core.Devices;
using PackInspect.Core.Domain;
using PackInspect.Core.Sequence;
using PackInspect.Tests.Fakes;

namespace PackInspect.Tests.Unit;

public class LotSessionTests
{
    private readonly InMemoryResultStore _store = new();
    private readonly LotSession _session;

    public LotSessionTests() => _session = new LotSession(_store, TimeProvider.System);

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Open_lot_starts_empty_and_open()
    {
        await _session.OpenAsync("LOT-1", TestRecipes.Resistance100, 3, TestToken);

        var lot = Assert.IsType<ActiveLot>(_session.Current);
        Assert.True(lot.IsOpen);
        Assert.Equal(0, lot.Total);
        Assert.True(lot.HasUnitsRemaining);
    }

    [Fact]
    public async Task Recording_units_updates_counts_and_yield()
    {
        await _session.OpenAsync("LOT-1", TestRecipes.Resistance100, 10, TestToken);

        await RecordAsync(1, Verdict.Pass);
        await RecordAsync(2, Verdict.Fail);
        await RecordAsync(3, Verdict.Pass);
        await RecordAsync(4, Verdict.Pass);

        var lot = _session.Current!;
        Assert.Equal(4, lot.Total);
        Assert.Equal(3, lot.Passed);
        Assert.Equal(1, lot.Failed);
        Assert.Equal(75.0, lot.YieldPercent);
        Assert.Equal(4, _store.Results.Count);
    }

    [Fact]
    public async Task Lot_closes_itself_when_the_quantity_is_reached()
    {
        await _session.OpenAsync("LOT-1", TestRecipes.Resistance100, 2, TestToken);

        await RecordAsync(1, Verdict.Pass);
        Assert.True(_session.Current!.IsOpen);

        await RecordAsync(2, Verdict.Pass);

        Assert.False(_session.Current!.IsOpen);
        Assert.False(_session.Current.HasUnitsRemaining);
        Assert.Single(_store.EndedLots);
    }

    [Fact]
    public async Task Second_lot_cannot_be_opened_while_one_is_open()
    {
        await _session.OpenAsync("LOT-1", TestRecipes.Resistance100, 2, TestToken);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _session.OpenAsync("LOT-2", TestRecipes.Resistance100, 2, TestToken));
    }

    [Fact]
    public async Task Lot_number_cannot_be_used_twice()
    {
        await _session.OpenAsync("LOT-1", TestRecipes.Resistance100, 2, TestToken);
        await _session.CloseAsync(TestToken);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _session.OpenAsync("LOT-1", TestRecipes.Resistance100, 2, TestToken));
    }

    [Fact]
    public async Task Invalid_lot_number_is_rejected_before_anything_is_stored() =>
        await Assert.ThrowsAsync<ArgumentException>(
            () => _session.OpenAsync("../bad", TestRecipes.Resistance100, 2, TestToken));

    [Fact]
    public async Task Yield_is_zero_before_the_first_unit()
    {
        await _session.OpenAsync("LOT-1", TestRecipes.Resistance100, 2, TestToken);

        Assert.Equal(0, _session.Current!.YieldPercent);
    }

    private Task RecordAsync(int sequence, Verdict final) =>
        _session.RecordAsync(
            new UnitResult(sequence, final, DefectType.None, 100, final, final, "image.png", 10, DateTimeOffset.UtcNow),
            new CapturedImage(1, 1, new byte[1]),
            TestToken);
}
