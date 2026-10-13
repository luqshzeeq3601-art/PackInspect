namespace PackInspect.Devices.Modbus;

/// <summary>Connection settings for the PLC, read from appsettings.json.</summary>
public sealed class ModbusOptions
{
    public string Host { get; set; } = "127.0.0.1";

    public int Port { get; set; } = 5020;

    public byte UnitId { get; set; } = 1;

    /// <summary>Longest time to wait for a connection or for the reply to one request.</summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>How often a driver re-reads status bits while waiting for a command to finish.</summary>
    public TimeSpan StatusPollInterval { get; set; } = TimeSpan.FromMilliseconds(50);
}
