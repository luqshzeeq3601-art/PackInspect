namespace PackInspect.Tests.Fakes;

/// <summary>Records what the fake devices were asked to do, in order, so a test can check the sequence.</summary>
internal sealed class StepLog
{
    private readonly Lock _gate = new();
    private readonly List<string> _steps = [];

    public IReadOnlyList<string> Steps
    {
        get
        {
            lock (_gate)
            {
                return [.. _steps];
            }
        }
    }

    public void Add(string step)
    {
        lock (_gate)
        {
            _steps.Add(step);
        }
    }
}
