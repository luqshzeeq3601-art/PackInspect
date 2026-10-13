using System.Globalization;
using System.Net.Sockets;
using PackInspect.Simulator.Net;
using PackInspect.Simulator.Units;

namespace PackInspect.Simulator.Instrument;

/// <summary>
/// A simulated bench multimeter that answers the SCPI commands in docs/Data.md.
/// It measures whatever unit is at the test position.
/// </summary>
internal sealed class ScpiServer(int port, StationModel station, SimulatorFaults faults) : TcpTextServer(port)
{
    private const string NoError = "0,\"No error\"";
    private string _lastError = NoError;

    protected override Task HandleLineAsync(string line, NetworkStream stream, CancellationToken cancellationToken)
    {
        if (faults.InstrumentNotResponding)
        {
            // Injected fault: the command is received and nothing comes back.
            return Task.CompletedTask;
        }

        switch (line.ToUpperInvariant())
        {
            case "*IDN?":
                return WriteLineAsync(stream, "PACKINSPECT,SIM-DMM,0001,1.0", cancellationToken);

            case "*RST":
                _lastError = NoError;
                return Task.CompletedTask;

            case "MEAS:RES?":
                return WriteLineAsync(stream, Format(station.MeasureResistance()), cancellationToken);

            case "MEAS:VOLT:DC?":
                return WriteLineAsync(stream, Format(station.MeasureVoltage()), cancellationToken);

            case "SYST:ERR?":
                string error = _lastError;
                _lastError = NoError;
                return WriteLineAsync(stream, error, cancellationToken);

            default:
                // As on a real instrument, an unknown command gives no reply; the
                // error is queued for SYST:ERR?.
                _lastError = "-113,\"Undefined header\"";
                return Task.CompletedTask;
        }
    }

    // SCPI numeric reply format, for example +9.98700000E+01.
    private static string Format(double value) =>
        value.ToString("+0.00000000E+00;-0.00000000E+00", CultureInfo.InvariantCulture);
}
