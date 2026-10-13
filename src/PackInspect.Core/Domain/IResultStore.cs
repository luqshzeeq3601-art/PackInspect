namespace PackInspect.Core.Domain;

/// <summary>Persistence for lots and unit results. Implemented with EF Core in PackInspect.Data.</summary>
public interface IResultStore
{
    /// <summary>Creates the lot and returns its id.</summary>
    /// <exception cref="InvalidOperationException">A lot with this number already exists.</exception>
    Task<int> StartLotAsync(string lotNumber, Recipe recipe, int quantity, DateTimeOffset startedAtUtc, int? operatorUserId, CancellationToken cancellationToken);

    Task<int> StartLotAsync(string lotNumber, Recipe recipe, int quantity, DateTimeOffset startedAtUtc, CancellationToken cancellationToken) =>
        StartLotAsync(lotNumber, recipe, quantity, startedAtUtc, null, cancellationToken);

    Task<int> SaveUnitResultAsync(int lotId, UnitResult result, CancellationToken cancellationToken);

    Task EndLotAsync(int lotId, DateTimeOffset endedAtUtc, CancellationToken cancellationToken);

    /// <summary>Counts the stored results of a lot. Yield is calculated, never stored.</summary>
    Task<LotSummary?> GetLotSummaryAsync(int lotId, CancellationToken cancellationToken);
}
