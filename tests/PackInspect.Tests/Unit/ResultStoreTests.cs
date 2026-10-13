using Microsoft.Extensions.DependencyInjection;
using PackInspect.Core.Domain;
using PackInspect.Data;
using PackInspect.Tests.Fakes;

namespace PackInspect.Tests.Unit;

/// <summary>The EF Core stores against a real SQLite file in a temporary folder.</summary>
public sealed class ResultStoreTests : IDisposable
{
    private readonly TempFolder _folder = new();
    private readonly ServiceProvider _services;

    public ResultStoreTests()
    {
        _services = new ServiceCollection()
            .AddSingleton(TimeProvider.System)
            .AddPackInspectData(_folder.File("test.db"))
            .BuildServiceProvider();
    }

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _services.Dispose();
        _folder.Dispose();
    }

    [Fact]
    public async Task New_database_is_created_with_the_starter_recipes()
    {
        var recipes = await InitializeAndGetRecipesAsync();

        Assert.Equal(3, recipes.Count);
        var recipe = Assert.Single(recipes, r => r.Name == "QFN-5x5-100R");
        Assert.Equal(new ElectricalLimits(95, 105), recipe.Limits);
        Assert.Equal(24, recipe.Vision.ExpectedLeadCount);
    }

    [Fact]
    public async Task Initializing_twice_does_not_duplicate_the_recipes()
    {
        await InitializeAndGetRecipesAsync();
        var recipes = await InitializeAndGetRecipesAsync();

        Assert.Equal(3, recipes.Count);
    }

    [Fact]
    public async Task Lot_summary_counts_stored_results_and_calculates_yield()
    {
        var recipe = (await InitializeAndGetRecipesAsync())[0];
        var store = _services.GetRequiredService<IResultStore>();

        int lotId = await store.StartLotAsync("LOT-A", recipe, quantity: 10, DateTimeOffset.UtcNow, TestToken);
        await store.SaveUnitResultAsync(lotId, Result(1, Verdict.Pass), TestToken);
        await store.SaveUnitResultAsync(lotId, Result(2, Verdict.Fail), TestToken);
        await store.SaveUnitResultAsync(lotId, Result(3, Verdict.Pass), TestToken);
        await store.SaveUnitResultAsync(lotId, Result(4, Verdict.Pass), TestToken);

        var summary = await store.GetLotSummaryAsync(lotId, TestToken);

        Assert.NotNull(summary);
        Assert.Equal("LOT-A", summary.LotNumber);
        Assert.Equal(recipe.Name, summary.RecipeName);
        Assert.Equal(10, summary.Quantity);
        Assert.Equal(4, summary.Total);
        Assert.Equal(3, summary.Passed);
        Assert.Equal(1, summary.Failed);
        Assert.Equal(75.0, summary.YieldPercent);
    }

    [Fact]
    public async Task Summary_of_an_empty_lot_has_zero_yield()
    {
        var recipe = (await InitializeAndGetRecipesAsync())[0];
        var store = _services.GetRequiredService<IResultStore>();
        int lotId = await store.StartLotAsync("LOT-EMPTY", recipe, 5, DateTimeOffset.UtcNow, TestToken);

        var summary = await store.GetLotSummaryAsync(lotId, TestToken);

        Assert.Equal(0, summary!.Total);
        Assert.Equal(0, summary.YieldPercent);
    }

    [Fact]
    public async Task Same_lot_number_cannot_be_started_twice()
    {
        var recipe = (await InitializeAndGetRecipesAsync())[0];
        var store = _services.GetRequiredService<IResultStore>();
        await store.StartLotAsync("LOT-A", recipe, 5, DateTimeOffset.UtcNow, TestToken);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.StartLotAsync("LOT-A", recipe, 5, DateTimeOffset.UtcNow, TestToken));
    }

    [Fact]
    public async Task Results_of_one_lot_do_not_count_towards_another()
    {
        var recipe = (await InitializeAndGetRecipesAsync())[0];
        var store = _services.GetRequiredService<IResultStore>();
        int first = await store.StartLotAsync("LOT-A", recipe, 5, DateTimeOffset.UtcNow, TestToken);
        int second = await store.StartLotAsync("LOT-B", recipe, 5, DateTimeOffset.UtcNow, TestToken);
        await store.SaveUnitResultAsync(first, Result(1, Verdict.Pass), TestToken);
        await store.SaveUnitResultAsync(second, Result(1, Verdict.Fail), TestToken);

        Assert.Equal(100.0, (await store.GetLotSummaryAsync(first, TestToken))!.YieldPercent);
        Assert.Equal(0.0, (await store.GetLotSummaryAsync(second, TestToken))!.YieldPercent);
    }

    [Fact]
    public async Task Summary_of_an_unknown_lot_is_null()
    {
        await InitializeAndGetRecipesAsync();

        Assert.Null(await _services.GetRequiredService<IResultStore>().GetLotSummaryAsync(999, TestToken));
    }

    private async Task<IReadOnlyList<Recipe>> InitializeAndGetRecipesAsync()
    {
        await _services.GetRequiredService<DatabaseInitializer>().InitializeAsync(TestToken);
        return await _services.GetRequiredService<IRecipeStore>().GetAllAsync(TestToken);
    }

    private static UnitResult Result(int sequence, Verdict final) =>
        new(sequence, final, DefectType.None, 100.0, Verdict.Pass, final, $"unit-{sequence}.png", 1200, DateTimeOffset.UtcNow);
}
