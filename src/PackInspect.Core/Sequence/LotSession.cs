using PackInspect.Core.Devices;
using PackInspect.Core.Domain;

namespace PackInspect.Core.Sequence;

/// <summary>
/// Tracks the lot currently on the machine: opens it, records each unit, and
/// closes it when the requested quantity has been tested.
/// </summary>
public sealed class LotSession(IResultStore store, TimeProvider time)
{
    private readonly Lock _gate = new();
    private ActiveLot? _current;
    private UnitResult? _lastResult;
    private CapturedImage? _lastImage;

    /// <summary>Raised on a background thread when the lot or its counts change.</summary>
    public event EventHandler? Changed;

    /// <summary>The lot on the machine. After it is closed it stays here, closed, so its summary remains visible.</summary>
    public ActiveLot? Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    public UnitResult? LastResult
    {
        get
        {
            lock (_gate)
            {
                return _lastResult;
            }
        }
    }

    public CapturedImage? LastImage
    {
        get
        {
            lock (_gate)
            {
                return _lastImage;
            }
        }
    }

    /// <exception cref="InvalidOperationException">A lot is already open, or the lot number was used before.</exception>
    public async Task OpenAsync(string lotNumber, Recipe recipe, int quantity, int? operatorUserId, CancellationToken cancellationToken)
    {
        if (LotRules.Validate(lotNumber, quantity) is { } problem)
        {
            throw new ArgumentException(problem);
        }

        if (Current is { IsOpen: true })
        {
            throw new InvalidOperationException("Close the current lot before opening another.");
        }

        int id = await store.StartLotAsync(lotNumber, recipe, quantity, time.GetUtcNow(), operatorUserId, cancellationToken).ConfigureAwait(false);
        lock (_gate)
        {
            _current = new ActiveLot(id, lotNumber, recipe, quantity, Total: 0, Passed: 0, IsOpen: true);
            _lastResult = null;
            _lastImage = null;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public Task OpenAsync(string lotNumber, Recipe recipe, int quantity, CancellationToken cancellationToken) =>
        OpenAsync(lotNumber, recipe, quantity, null, cancellationToken);

    /// <summary>Saves one unit result and closes the lot when the quantity is reached.</summary>
    public async Task<int> RecordAsync(UnitResult result, CapturedImage image, CancellationToken cancellationToken)
    {
        var lot = Current is { IsOpen: true } open ? open : throw new InvalidOperationException("No lot is open.");

        int unitResultId = await store.SaveUnitResultAsync(lot.Id, result, cancellationToken).ConfigureAwait(false);

        var updated = lot with
        {
            Total = lot.Total + 1,
            Passed = lot.Passed + (result.FinalVerdict == Verdict.Pass ? 1 : 0),
        };
        if (updated.Total >= updated.Quantity)
        {
            await store.EndLotAsync(lot.Id, time.GetUtcNow(), cancellationToken).ConfigureAwait(false);
            updated = updated with { IsOpen = false };
        }

        lock (_gate)
        {
            _current = updated;
            _lastResult = result;
            _lastImage = image;
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return unitResultId;
    }

    /// <summary>Closes the lot early, when the operator ends it before the quantity is reached.</summary>
    public async Task CloseAsync(CancellationToken cancellationToken)
    {
        if (Current is not { IsOpen: true } lot)
        {
            return;
        }

        await store.EndLotAsync(lot.Id, time.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        lock (_gate)
        {
            _current = lot with { IsOpen = false };
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }
}
