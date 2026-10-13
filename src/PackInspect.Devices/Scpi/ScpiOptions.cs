namespace PackInspect.Devices.Scpi;

/// <summary>Connection settings for the multimeter, read from appsettings.json.</summary>
public sealed class ScpiOptions
{
    public string Host { get; set; } = "127.0.0.1";

    public int Port { get; set; } = 5025;

    /// <summary>Longest time to wait for the reply to one query.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(2);
}
