using PackInspect.Core.Machine;

namespace PackInspect.Tests.Fakes;

/// <summary>
/// Waits for the machine to reach a condition by listening to its events, so
/// tests never sleep for a guessed amount of time.
/// </summary>
internal static class MachineWait
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    public static Task ForStateAsync(MachineController controller, MachineState state) =>
        UntilAsync(controller, () => controller.State == state, $"state {state}");

    public static async Task UntilAsync(
        MachineController controller,
        Func<bool> condition,
        string description,
        TimeSpan? timeout = null)
    {
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        void Check(object? sender, EventArgs e)
        {
            if (condition())
            {
                reached.TrySetResult();
            }
        }

        controller.StateChanged += Check;
        controller.AlarmsChanged += Check;
        controller.StatusChanged += Check;
        try
        {
            Check(null, EventArgs.Empty);
            await reached.Task.WaitAsync(timeout ?? DefaultTimeout, TestContext.Current.CancellationToken);
        }
        catch (TimeoutException)
        {
            Assert.Fail($"Timed out waiting for {description}. State is {controller.State}, " +
                $"alarms: [{string.Join(", ", controller.ActiveAlarms.Select(alarm => alarm.Code))}].");
        }
        finally
        {
            controller.StateChanged -= Check;
            controller.AlarmsChanged -= Check;
            controller.StatusChanged -= Check;
        }
    }
}
