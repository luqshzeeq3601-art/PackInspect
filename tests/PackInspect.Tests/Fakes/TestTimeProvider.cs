namespace PackInspect.Tests.Fakes;

public sealed class TestTimeProvider(DateTimeOffset initial) : TimeProvider
{
    private DateTimeOffset _current = initial;

    public override DateTimeOffset GetUtcNow() => _current;

    public void Advance(TimeSpan duration) => _current += duration;
}
