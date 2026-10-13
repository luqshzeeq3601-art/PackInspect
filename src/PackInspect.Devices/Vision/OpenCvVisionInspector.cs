using OpenCvSharp;
using PackInspect.Core.Devices;
using PackInspect.Core.Domain;
using PackInspect.Core.Sequence;

namespace PackInspect.Devices.Vision;

/// <summary>
/// Rule-based inspection with OpenCV. The package has three brightness levels
/// (dark body, mid-grey carrier, bright leads and marking), so simple
/// thresholds separate them. Four checks run in order and the first that fails
/// names the defect:
/// 1. body shape (chipped corner), 2. lead count, 3. marking present,
/// 4. anything bright on the body that is not the marking (scratch or contamination).
/// The limits for each check come from the recipe.
/// </summary>
public sealed class OpenCvVisionInspector : IVisionInspector
{
    // Grey levels that separate the three brightness classes.
    private const double DarkThreshold = 78;
    private const double SurfaceThreshold = 85;
    private const double LeadThreshold = 160;

    private const double MinBodyAreaPx = 10_000;
    private const double MinLeadAreaPx = 60;

    // The surface checks stay this far inside the body's bounding box, so a
    // slightly rotated unit does not bring the carrier into view at the corners.
    private const int SurfaceMarginPx = 10;

    // Where the marking is printed, as a fraction of the body's bounding box.
    private const double MarkingLeft = 0.18;
    private const double MarkingRight = 0.82;
    private const double MarkingTop = 0.34;
    private const double MarkingBottom = 0.70;

    // A blob at least this many times longer than wide is called a scratch.
    private const double ScratchElongation = 3.0;

    public VisionResult Inspect(CapturedImage image, VisionSettings settings)
    {
        if (image.Width <= 0 || image.Height <= 0 || image.Pixels.Length != image.Width * image.Height)
        {
            throw new ArgumentException("The image size does not match its pixel data.", nameof(image));
        }

        using var source = Mat.FromPixelData(image.Height, image.Width, MatType.CV_8UC1, image.Pixels.ToArray());
        using var smooth = new Mat();
        Cv2.GaussianBlur(source, smooth, new Size(3, 3), 0);

        // 1. Body: the largest dark region.
        using var dark = new Mat();
        Cv2.Threshold(smooth, dark, DarkThreshold, 255, ThresholdTypes.BinaryInv);
        Cv2.FindContours(dark, out Point[][] darkContours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
        var body = darkContours.MaxBy(contour => Cv2.ContourArea(contour));
        if (body is null || Cv2.ContourArea(body) < MinBodyAreaPx)
        {
            return VisionResult.NoUnit;
        }

        // Count the pixels the body covers (outline filled in, so the bright
        // marking inside does not count as missing). A perfect rectangle fills
        // its best-fit rectangle completely; a chipped corner does not.
        using var filled = new Mat(dark.Size(), MatType.CV_8UC1, Scalar.Black);
        Cv2.DrawContours(filled, [body], 0, Scalar.White, thickness: -1);
        var fitted = Cv2.MinAreaRect(body);
        double rectangularity = Cv2.CountNonZero(filled) / ((fitted.Size.Width + 1) * (fitted.Size.Height + 1));
        var box = Cv2.BoundingRect(body);
        if (box.Width <= 4 * SurfaceMarginPx || box.Height <= 4 * SurfaceMarginPx)
        {
            return VisionResult.NoUnit;
        }

        int leadCount = CountLeads(smooth, box);
        var (markingPixels, defectArea, largestDefectElongation) = MeasureSurface(smooth, box);

        var defect = DefectType.None;
        if (rectangularity < settings.MinBodyRectangularity)
        {
            defect = DefectType.CrackChip;
        }
        else if (leadCount != settings.ExpectedLeadCount)
        {
            defect = DefectType.LeadDefect;
        }
        else if (markingPixels < settings.MinMarkingPixels)
        {
            defect = DefectType.MarkingDefect;
        }
        else if (markingPixels > settings.MaxMarkingPixels)
        {
            // More bright area than the marking alone can explain: something lies on top of it.
            defect = DefectType.Contamination;
        }
        else if (defectArea > settings.MaxDefectAreaPx)
        {
            defect = largestDefectElongation >= ScratchElongation ? DefectType.Scratch : DefectType.Contamination;
        }

        return new VisionResult(
            UnitFound: true,
            Verdict: defect == DefectType.None ? Verdict.Pass : Verdict.Fail,
            Defect: defect,
            BodyRectangularity: rectangularity,
            LeadCount: leadCount,
            MarkingPixels: markingPixels,
            DefectAreaPx: defectArea);
    }

    // 2. Leads: bright blobs outside the body.
    private static int CountLeads(Mat smooth, Rect box)
    {
        using var bright = new Mat();
        Cv2.Threshold(smooth, bright, LeadThreshold, 255, ThresholdTypes.Binary);

        // Blank the body so the marking, which is just as bright, is not counted.
        var inside = Shrink(box, 4);
        using (var bodyRegion = new Mat(bright, inside))
        {
            bodyRegion.SetTo(Scalar.Black);
        }

        Cv2.FindContours(bright, out Point[][] contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
        return contours.Count(contour => Cv2.ContourArea(contour) >= MinLeadAreaPx);
    }

    // 3 and 4. Surface: bright pixels inside the marking window are the marking;
    // bright pixels anywhere else on the body are a defect.
    private static (int MarkingPixels, int DefectArea, double LargestElongation) MeasureSurface(Mat smooth, Rect box)
    {
        var surface = Shrink(box, SurfaceMarginPx);
        using var surfaceView = new Mat(smooth, surface);
        using var marks = new Mat();
        Cv2.Threshold(surfaceView, marks, SurfaceThreshold, 255, ThresholdTypes.Binary);

        // The marking window, converted from the bounding box to surface coordinates.
        var window = Rect.FromLTRB(
            box.Left + (int)(box.Width * MarkingLeft) - surface.Left,
            box.Top + (int)(box.Height * MarkingTop) - surface.Top,
            box.Left + (int)(box.Width * MarkingRight) - surface.Left,
            box.Top + (int)(box.Height * MarkingBottom) - surface.Top);

        int markingPixels;
        using (var markingView = new Mat(marks, window))
        {
            markingPixels = Cv2.CountNonZero(markingView);
            markingView.SetTo(Scalar.Black);
        }

        int defectArea = Cv2.CountNonZero(marks);
        double elongation = 0;
        if (defectArea > 0)
        {
            Cv2.FindContours(marks, out Point[][] contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
            var size = Cv2.MinAreaRect(contours.MaxBy(contour => contour.Length)!).Size;
            elongation = Math.Max(size.Width, size.Height) / Math.Max(Math.Min(size.Width, size.Height), 1);
        }

        return (markingPixels, defectArea, elongation);
    }

    private static Rect Shrink(Rect rect, int margin) =>
        new(rect.X + margin, rect.Y + margin, rect.Width - (2 * margin), rect.Height - (2 * margin));
}
