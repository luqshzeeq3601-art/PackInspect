namespace PackInspect.Core.Devices;

/// <summary>
/// The electrical test instrument (a multimeter). First-cut contract;
/// implemented over SCPI in phase 2. The interface speaks in measurements, not
/// SCPI strings, so the protocol stays inside the driver.
/// </summary>
public interface IInstrument
{
    Task ConnectAsync(CancellationToken cancellationToken);

    Task DisconnectAsync(CancellationToken cancellationToken);

    /// <summary>Returns the instrument's identification string.</summary>
    Task<string> IdentifyAsync(CancellationToken cancellationToken);

    /// <summary>Takes one reading in SI units (volts or ohms).</summary>
    Task<double> MeasureAsync(MeasurementKind kind, CancellationToken cancellationToken);
}
