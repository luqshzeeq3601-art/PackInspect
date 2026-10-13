namespace PackInspect.Core.Devices;

/// <summary>
/// The machine PLC: safety interlocks and utilities.
/// The sequence and the UI talk to this interface only, so a real PLC driver
/// can replace the simulated one without changing either.
/// </summary>
public interface IPlc
{
    Task ConnectAsync(CancellationToken cancellationToken);

    Task DisconnectAsync(CancellationToken cancellationToken);

    /// <summary>Reads all interlock inputs in one request so they are consistent with each other.</summary>
    Task<PlcInputs> ReadInputsAsync(CancellationToken cancellationToken);

    /// <summary>Asks the PLC to clear its latched faults.</summary>
    Task ResetFaultsAsync(CancellationToken cancellationToken);

    /// <summary>Has the handler place the next unit on the carrier; completes when the unit is present.</summary>
    Task LoadUnitAsync(CancellationToken cancellationToken);

    /// <summary>Has the handler remove the unit and put it in the given bin.</summary>
    Task UnloadUnitAsync(SortBin bin, CancellationToken cancellationToken);
}
