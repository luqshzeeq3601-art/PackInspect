using Microsoft.Extensions.DependencyInjection;
using PackInspect.Core.Audit;
using PackInspect.Core.Auth;
using PackInspect.Core.Devices;
using PackInspect.Core.Domain;
using PackInspect.Data;
using PackInspect.Tests.Fakes;

namespace PackInspect.Tests.Unit;

public sealed class RecipeSignOffTests : IDisposable
{
    private readonly TempFolder _folder = new();
    private readonly ServiceProvider _services;

    public RecipeSignOffTests()
    {
        _services = new ServiceCollection()
            .AddSingleton(TimeProvider.System)
            .AddPackInspectData(_folder.File("recipe_signoff.db"))
            .BuildServiceProvider();
    }

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _services.Dispose();
        _folder.Dispose();
    }

    [Fact]
    public async Task Recipe_change_requires_a_non_empty_reason()
    {
        await _services.GetRequiredService<DatabaseInitializer>().InitializeAsync(TestToken);
        var recipeStore = _services.GetRequiredService<IRecipeStore>();
        var recipe = (await recipeStore.GetAllAsync(TestToken))[0];

        await Assert.ThrowsAsync<ArgumentException>(
            () => recipeStore.SaveWithSignOffAsync(recipe, engineerUserId: 1, reason: "", TestToken));

        await Assert.ThrowsAsync<ArgumentException>(
            () => recipeStore.SaveWithSignOffAsync(recipe, engineerUserId: 1, reason: "   ", TestToken));
    }

    [Fact]
    public async Task Recipe_change_increments_version_and_records_audit_trail_for_each_changed_field()
    {
        await _services.GetRequiredService<DatabaseInitializer>().InitializeAsync(TestToken);
        var recipeStore = _services.GetRequiredService<IRecipeStore>();
        var auditStore = _services.GetRequiredService<IAuditStore>();
        var recipe = (await recipeStore.GetAllAsync(TestToken))[0];

        int initialVersion = recipe.Version;
        var modified = recipe with
        {
            Limits = new ElectricalLimits(92.0, 108.0),
            CameraPositionMm = 105.0,
        };

        const string Reason = "Tighten tolerance after machine qualification";
        var updated = await recipeStore.SaveWithSignOffAsync(modified, engineerUserId: 2, reason: Reason, TestToken);

        Assert.Equal(initialVersion + 1, updated.Version);
        Assert.Equal(92.0, updated.Limits.Low);
        Assert.Equal(108.0, updated.Limits.High);
        Assert.Equal(105.0, updated.CameraPositionMm);
        Assert.Equal(2, updated.UpdatedByUserId);

        var auditEntries = await auditStore.GetRecentAsync(50, TestToken);
        var recipeAudits = auditEntries.Where(e => e.Entity == "Recipe" && e.EntityId == recipe.Id && e.Action == "RecipeChanged").ToList();

        Assert.Contains(recipeAudits, a => a.Field == "LowLimit" && a.Reason == Reason && a.UserId == 2);
        Assert.Contains(recipeAudits, a => a.Field == "HighLimit" && a.Reason == Reason && a.UserId == 2);
        Assert.Contains(recipeAudits, a => a.Field == "CameraPositionMm" && a.Reason == Reason && a.UserId == 2);
    }

    [Fact]
    public async Task New_recipe_creation_with_sign_off_starts_at_version_1_and_is_audited()
    {
        await _services.GetRequiredService<DatabaseInitializer>().InitializeAsync(TestToken);
        var recipeStore = _services.GetRequiredService<IRecipeStore>();
        var auditStore = _services.GetRequiredService<IAuditStore>();

        var newRecipe = new Recipe
        {
            Name = "QFN-6x6-50R",
            Version = 1,
            CameraPositionMm = 120.0,
            TestPositionMm = 210.0,
            Vision = new VisionSettings(0.97, 28, 1000, 2000, 20),
            MeasurementKind = MeasurementKind.Resistance,
            Limits = new ElectricalLimits(48.0, 52.0),
        };

        const string Reason = "New product introduction package 6x6";
        var created = await recipeStore.CreateWithSignOffAsync(newRecipe, engineerUserId: 2, reason: Reason, TestToken);

        Assert.Equal(1, created.Version);
        Assert.Equal("QFN-6x6-50R", created.Name);

        var auditEntries = await auditStore.GetRecentAsync(10, TestToken);
        Assert.Contains(auditEntries, a => a.Action == "RecipeCreated" && a.EntityId == created.Id && a.Reason == Reason);
    }
}
