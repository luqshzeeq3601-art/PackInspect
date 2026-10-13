using PackInspect.Core.Devices;
using PackInspect.Devices.Net;

namespace PackInspect.Devices.Scpi;

/// <summary>
/// A multimeter driven with SCPI commands over a TCP socket (port 5025 is the
/// usual raw SCPI port). The command strings stay inside this class.
/// </summary>
public sealed class ScpiInstrument(ScpiOptions options) : IInstrument, IDisposable
{
    private const int MaxReplyLength = 256;
    private readonly TcpTextClient _client = new(options.Host, options.Port, options.Timeout);

    /// <summary>Opens the connection and checks that an instrument answers.</summary>
    public Task ConnectAsync(CancellationToken cancellationToken) => IdentifyAsync(cancellationToken);

    public Task DisconnectAsync(CancellationToken cancellationToken) => _client.CloseAsync(cancellationToken);

    public Task<string> IdentifyAsync(CancellationToken cancellationToken) => QueryAsync("*IDN?", cancellationToken);

    public async Task<double> MeasureAsync(MeasurementKind kind, CancellationToken cancellationToken)
    {
        string command = kind switch
        {
            MeasurementKind.DcVoltage => "MEAS:VOLT:DC?",
            MeasurementKind.Resistance => "MEAS:RES?",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported measurement."),
        };

        return ScpiReply.ParseNumber(await QueryAsync(command, cancellationToken).ConfigureAwait(false));
    }

    public void Dispose() => _client.Dispose();

    private Task<string> QueryAsync(string command, CancellationToken cancellationToken) =>
        _client.ExchangeAsync(
            async (stream, token) =>
            {
                await TcpTextClient.WriteLineAsync(stream, command, token).ConfigureAwait(false);
                return await TcpTextClient.ReadLineAsync(stream, MaxReplyLength, token).ConfigureAwait(false);
            },
            cancellationToken);
}
