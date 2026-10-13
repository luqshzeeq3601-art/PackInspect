using Microsoft.Extensions.Logging;

namespace PackInspect.Tests.Fakes;

/// <summary>Writes log lines to the running test's output, so a failed test shows what the machine logged.</summary>
internal sealed class TestOutputLogger<T> : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var line = $"[{logLevel}] {formatter(state, exception)}";
        TestContext.Current.TestOutputHelper?.WriteLine(exception is null ? line : $"{line}{Environment.NewLine}{exception}");
    }
}
