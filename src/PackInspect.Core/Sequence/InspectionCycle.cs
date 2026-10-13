using PackInspect.Core.Ai;
using PackInspect.Core.Alarms;
using PackInspect.Core.Devices;
using PackInspect.Core.Domain;
using PackInspect.Core.Machine;
using PackInspect.Core.Publishing;

namespace PackInspect.Core.Sequence;

/// <summary>
/// One machine cycle for one unit: load, move to camera, capture, inspect,
/// read barcode, move to test, measure, sort and unload, save result, and publish payload.
/// It depends only on interfaces, so the same code runs against the simulator,
/// against fakes in unit tests, and would run against real hardware.
/// </summary>
public sealed class InspectionCycle(
    IPlc plc,
    IMotionAxis axis,
    ICamera camera,
    IVisionInspector inspector,
    IInstrument instrument,
    IImageStore images,
    LotSession lots,
    SequenceOptions options,
    TimeProvider time,
    IBarcodeReader? barcodeReader = null,
    IResultPublisher? resultPublisher = null,
    IDefectClassifier? defectClassifier = null,
    IAiReviewStore? aiReviewStore = null) : IMachineCycle
{
    public bool HasMoreWork => lots.Current is { HasUnitsRemaining: true };

    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        var lot = lots.Current is { IsOpen: true } open
            ? open
            : throw new MachineFaultException(AlarmCodes.CycleFault, "The cycle was started without an open lot.");
        var recipe = lot.Recipe;
        long started = time.GetTimestamp();
        int sequence = lot.Total + 1;

        await MoveAsync(options.LoadPositionMm, cancellationToken).ConfigureAwait(false);
        await StepAsync(AlarmCodes.HandlerFault, "Loading the unit failed", () => plc.LoadUnitAsync(cancellationToken), cancellationToken).ConfigureAwait(false);

        // Barcode reader scans the newly placed package unit
        string? barcode = null;
        if (barcodeReader is not null)
        {
            barcode = await StepAsync(
                AlarmCodes.BarcodeReaderFault,
                "Reading the unit barcode failed",
                () => barcodeReader.ReadBarcodeAsync(sequence, cancellationToken),
                cancellationToken).ConfigureAwait(false);
        }

        await MoveAsync(recipe.CameraPositionMm, cancellationToken).ConfigureAwait(false);
        var image = await StepAsync(AlarmCodes.CameraNoResponse, "The camera did not deliver an image", () => camera.CaptureAsync(cancellationToken), cancellationToken).ConfigureAwait(false);

        var vision = inspector.Inspect(image, recipe.Vision);
        if (!vision.UnitFound)
        {
            throw new MachineFaultException(AlarmCodes.UnitNotFound, "No unit was found in the camera image.");
        }

        await MoveAsync(recipe.TestPositionMm, cancellationToken).ConfigureAwait(false);
        double measured = await StepAsync(AlarmCodes.InstrumentNoResponse, "The instrument did not return a reading", () => instrument.MeasureAsync(recipe.MeasurementKind, cancellationToken), cancellationToken).ConfigureAwait(false);

        var electrical = recipe.Limits.Evaluate(measured);

        // A unit is good only if it passes both the visual and the electrical check.
        var final = vision.Verdict == Verdict.Pass && electrical == Verdict.Pass ? Verdict.Pass : Verdict.Fail;

        var bin = final == Verdict.Pass ? SortBin.Pass : SortBin.Reject;
        await StepAsync(AlarmCodes.HandlerFault, "Unloading the unit failed", () => plc.UnloadUnitAsync(bin, cancellationToken), cancellationToken).ConfigureAwait(false);

        await StepAsync(
            AlarmCodes.ResultNotSaved,
            "The unit result could not be saved",
            async () =>
            {
                string imagePath = await images.SaveAsync(lot.Id, sequence, image, cancellationToken).ConfigureAwait(false);
                var result = new UnitResult(
                    sequence,
                    vision.Verdict,
                    vision.Defect,
                    measured,
                    electrical,
                    final,
                    imagePath,
                    (int)time.GetElapsedTime(started).TotalMilliseconds,
                    time.GetUtcNow(),
                    barcode);
                int unitResultId = await lots.RecordAsync(result, image, cancellationToken).ConfigureAwait(false);

                if (defectClassifier is not null && aiReviewStore is not null)
                {
                    var classification = await defectClassifier.ClassifyAsync(image, cancellationToken).ConfigureAwait(false);
                    await aiReviewStore.SaveAiClassificationAsync(
                        unitResultId,
                        classification.Label,
                        classification.Confidence,
                        classification.ModelVersion,
                        cancellationToken).ConfigureAwait(false);
                }

                if (resultPublisher is not null)
                {
                    var payload = PublishedResultPayload.Create("PACKINSPECT-SIM-01", lot.LotNumber, recipe, result);
                    await resultPublisher.PublishAsync(payload, cancellationToken).ConfigureAwait(false);
                }
            },
            cancellationToken).ConfigureAwait(false);
    }

    private async Task MoveAsync(double positionMm, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.MoveTimeout);
        try
        {
            await axis.MoveAbsoluteAsync(positionMm, options.AxisSpeedMmPerSec, timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new MachineFaultException(
                AlarmCodes.AxisMoveTimeout,
                $"Axis {axis.Name} did not reach {positionMm:0.###} mm within {options.MoveTimeout.TotalSeconds:0.#} s.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new MachineFaultException(AlarmCodes.AxisFault, $"Axis {axis.Name} move to {positionMm:0.###} mm failed: {ex.Message}", ex);
        }
    }

    // Runs one device step. Cancellation passes straight through (it means "stop",
    // not "fault"); any other failure becomes the alarm that names the device.
    private static async Task<T> StepAsync<T>(string alarmCode, string what, Func<Task<T>> step, CancellationToken cancellationToken)
    {
        try
        {
            return await step().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not MachineFaultException && !cancellationToken.IsCancellationRequested)
        {
            throw new MachineFaultException(alarmCode, $"{what}: {ex.Message}", ex);
        }
    }

    private static Task StepAsync(string alarmCode, string what, Func<Task> step, CancellationToken cancellationToken) =>
        StepAsync(
            alarmCode,
            what,
            async () =>
            {
                await step().ConfigureAwait(false);
                return true;
            },
            cancellationToken);
}
