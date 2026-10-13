using OpenCvSharp;
using PackInspect.Simulator.Units;

namespace PackInspect.Simulator.Imaging;

/// <summary>
/// Draws a synthetic top view of a package unit: dark body, bright leads on
/// four sides, bright marking, and optionally one defect. Position, rotation,
/// lighting and sensor noise vary from image to image, as they would on a
/// machine. The same seed always gives the same image.
/// </summary>
public static class UnitImageGenerator
{
    public const int ImageSize = 320;

    private const int BodySize = 200;
    private const int BodyStart = (ImageSize - BodySize) / 2;
    private const int BodyEnd = BodyStart + BodySize;
    private const int LeadsPerSide = 6;
    private const int LeadWidth = 14;
    private const int LeadLength = 16;

    private const byte BackgroundGray = 110;
    private const byte BodyGray = 45;
    private const byte LeadGray = 215;
    private const byte MarkingGray = 200;

    public static UnitImage Generate(UnitDefect defect, int seed)
    {
        var random = new Random(seed);
        using var canvas = new Mat(ImageSize, ImageSize, MatType.CV_8UC1, new Scalar(BackgroundGray));

        DrawLeads(canvas, missingLead: defect == UnitDefect.LeadDefect ? random.Next(LeadsPerSide * 4) : -1);
        Cv2.Rectangle(canvas, new Rect(BodyStart, BodyStart, BodySize, BodySize), new Scalar(BodyGray), thickness: -1);
        DrawMarking(canvas, defect == UnitDefect.MarkingDefect, random);

        switch (defect)
        {
            case UnitDefect.Scratch:
                DrawScratch(canvas, random);
                break;
            case UnitDefect.Contamination:
                DrawContamination(canvas, random);
                break;
            case UnitDefect.CrackChip:
                DrawChippedCorner(canvas, random);
                break;
        }

        return Finish(canvas, random, placeUnit: true);
    }

    /// <summary>An empty carrier: background only, with the same lighting and noise.</summary>
    public static UnitImage GenerateEmpty(int seed)
    {
        using var canvas = new Mat(ImageSize, ImageSize, MatType.CV_8UC1, new Scalar(BackgroundGray));
        return Finish(canvas, new Random(seed), placeUnit: false);
    }

    private static void DrawLeads(Mat canvas, int missingLead)
    {
        double pitch = (double)BodySize / LeadsPerSide;
        for (int index = 0; index < LeadsPerSide * 4; index++)
        {
            if (index == missingLead)
            {
                continue;
            }

            int side = index / LeadsPerSide;
            int along = (int)(BodyStart + (pitch * ((index % LeadsPerSide) + 0.5)) - (LeadWidth / 2.0));
            var lead = side switch
            {
                0 => new Rect(along, BodyStart - LeadLength, LeadWidth, LeadLength),
                1 => new Rect(along, BodyEnd, LeadWidth, LeadLength),
                2 => new Rect(BodyStart - LeadLength, along, LeadLength, LeadWidth),
                _ => new Rect(BodyEnd, along, LeadLength, LeadWidth),
            };
            Cv2.Rectangle(canvas, lead, new Scalar(LeadGray), thickness: -1);
        }
    }

    private static void DrawMarking(Mat canvas, bool defective, Random random)
    {
        // A marking defect is either no marking at all or only the short second line.
        bool drawFirstLine = !defective;
        bool drawSecondLine = !defective || random.Next(2) == 0;

        if (drawFirstLine)
        {
            Cv2.PutText(canvas, "PI-QFN", new Point(BodyStart + 46, BodyStart + 100), HersheyFonts.HersheySimplex, 0.9, new Scalar(MarkingGray), 2, LineTypes.AntiAlias);
        }

        if (drawSecondLine)
        {
            Cv2.PutText(canvas, "L2641", new Point(BodyStart + 70, BodyStart + 128), HersheyFonts.HersheySimplex, 0.6, new Scalar(MarkingGray), 1, LineTypes.AntiAlias);
        }
    }

    private static void DrawScratch(Mat canvas, Random random)
    {
        const int Margin = 15;
        const double MinLength = 50;
        const double MaxLength = 140;

        // Two random points on the body, redrawn until the line between them has a realistic length.
        Point start;
        Point end;
        double length;
        do
        {
            start = new Point(random.Next(BodyStart + Margin, BodyEnd - Margin), random.Next(BodyStart + Margin, BodyEnd - Margin));
            end = new Point(random.Next(BodyStart + Margin, BodyEnd - Margin), random.Next(BodyStart + Margin, BodyEnd - Margin));
            length = start.DistanceTo(end);
        }
        while (length is < MinLength or > MaxLength);

        Cv2.Line(canvas, start, end, new Scalar(random.Next(190, 231)), random.Next(1, 3), LineTypes.AntiAlias);
    }

    private static void DrawContamination(Mat canvas, Random random)
    {
        const int Margin = 25;
        var centre = new Point(random.Next(BodyStart + Margin, BodyEnd - Margin), random.Next(BodyStart + Margin, BodyEnd - Margin));
        var axes = new Size(random.Next(6, 15), random.Next(6, 15));
        Cv2.Ellipse(canvas, centre, axes, random.Next(180), 0, 360, new Scalar(random.Next(130, 191)), thickness: -1, LineTypes.AntiAlias);
    }

    private static void DrawChippedCorner(Mat canvas, Random random)
    {
        int legX = random.Next(55, 76);
        int legY = random.Next(55, 76);
        bool right = random.Next(2) == 0;
        bool bottom = random.Next(2) == 0;
        int cornerX = right ? BodyEnd : BodyStart;
        int cornerY = bottom ? BodyEnd : BodyStart;
        Point[] triangle =
        [
            new(cornerX, cornerY),
            new(cornerX + (right ? -legX : legX), cornerY),
            new(cornerX, cornerY + (bottom ? -legY : legY)),
        ];

        // The missing corner shows the carrier underneath.
        Cv2.FillConvexPoly(canvas, triangle, new Scalar(BackgroundGray));
    }

    private static UnitImage Finish(Mat canvas, Random random, bool placeUnit)
    {
        using var placed = new Mat();
        if (placeUnit)
        {
            // Pick-and-place error: a few pixels of offset and a couple of degrees of rotation.
            double angle = (random.NextDouble() * 4) - 2;
            using var transform = Cv2.GetRotationMatrix2D(new Point2f(ImageSize / 2f, ImageSize / 2f), angle, 1.0);
            transform.Set(0, 2, transform.At<double>(0, 2) + random.Next(-6, 7));
            transform.Set(1, 2, transform.At<double>(1, 2) + random.Next(-6, 7));
            Cv2.WarpAffine(canvas, placed, transform, canvas.Size(), InterpolationFlags.Linear, BorderTypes.Constant, new Scalar(BackgroundGray));
        }
        else
        {
            canvas.CopyTo(placed);
        }

        // Lens blur, then a lighting change of up to 10 percent.
        using var blurred = new Mat();
        Cv2.GaussianBlur(placed, blurred, new Size(3, 3), 0.8);
        using var lit = new Mat();
        blurred.ConvertTo(lit, MatType.CV_8UC1, alpha: 0.9 + (random.NextDouble() * 0.2));

        if (!lit.GetArray(out byte[] pixels))
        {
            throw new InvalidOperationException("Could not read the generated image.");
        }

        // Sensor noise from our own seeded generator, so images are reproducible.
        const double NoiseSigma = 4;
        for (int i = 0; i < pixels.Length; i++)
        {
            double noise = Math.Sqrt(-2.0 * Math.Log(1.0 - random.NextDouble())) * Math.Cos(2.0 * Math.PI * random.NextDouble()) * NoiseSigma;
            pixels[i] = (byte)Math.Clamp(pixels[i] + noise, 0, 255);
        }

        return new UnitImage(ImageSize, ImageSize, pixels);
    }
}
