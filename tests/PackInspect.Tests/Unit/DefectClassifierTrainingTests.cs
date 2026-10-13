using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.ML;
using Microsoft.ML.Trainers;
using PackInspect.Core.Devices;
using PackInspect.Core.Domain;
using PackInspect.Devices.Ai;
using PackInspect.Devices.Vision;
using PackInspect.Simulator.Imaging;
using PackInspect.Simulator.Units;
using PackInspect.Tests.Fakes;

namespace PackInspect.Tests.Unit;

public class DefectClassifierTrainingTests
{
    // In the order of the DefectType enum, so a class index is also the enum value.
    private static readonly (UnitDefect Defect, DefectType Type)[] Classes =
    [
        (UnitDefect.None, DefectType.None),
        (UnitDefect.Scratch, DefectType.Scratch),
        (UnitDefect.CrackChip, DefectType.CrackChip),
        (UnitDefect.Contamination, DefectType.Contamination),
        (UnitDefect.MarkingDefect, DefectType.MarkingDefect),
        (UnitDefect.LeadDefect, DefectType.LeadDefect),
    ];

    /// <summary>A small, fast check that the pipeline works end to end. It writes no files.</summary>
    [Fact]
    public async Task Train_and_evaluate_multiclass_head_on_dinov2_embeddings()
    {
        using var extractor = new Dinov2FeatureExtractor(AiModelFiles.BackboneOrSkip());

        const int TrainPerClass = 25;
        const int TestPerClass = 10;
        var train = new List<PackageEmbeddingData>();
        var test = new List<PackageEmbeddingData>();

        foreach (var (defect, type) in Classes)
        {
            for (int i = 0; i < TrainPerClass; i++)
            {
                train.Add(Embed(extractor, Capture(defect, seed: 1000 + i), type));
            }

            for (int i = 0; i < TestPerClass; i++)
            {
                test.Add(Embed(extractor, Capture(defect, seed: 5000 + i), type));
            }
        }

        var mlContext = new MLContext(seed: 42);
        var model = TrainHead(mlContext, train);

        var metrics = mlContext.MulticlassClassification.Evaluate(model.Transform(mlContext.Data.LoadFromEnumerable(test)));
        Assert.True(metrics.MicroAccuracy >= 0.75, $"Expected >= 75% micro accuracy, but got {metrics.MicroAccuracy:P2}");

        using var classifier = new Dinov2DefectClassifier(extractor, model);
        var result = await classifier.ClassifyAsync(Capture(UnitDefect.Scratch, seed: 9999), TestContext.Current.CancellationToken);
        Assert.Equal(DefectType.Scratch, result.Label);
        Assert.True(result.Confidence > 0.5);
    }

    /// <summary>
    /// Trains the head on the dataset docs/PRD.md section 7 describes, scores it on
    /// images it has never seen, and writes the measured numbers to
    /// models/training-report.md. It takes minutes, so it only runs when asked for.
    /// </summary>
    [Fact(Explicit = true)]
    public void Full_dataset_training_meets_the_accuracy_target_and_saves_the_head()
    {
        const int ImagesPerClass = 600;
        const int TrainPerClass = 420;      // 70 %
        const int ValidationPerClass = 90;  // 15 %; the remaining 90 are the held-out test set
        const double AccuracyTarget = 0.95;

        string backbonePath = AiModelFiles.BackboneOrSkip();
        using var extractor = new Dinov2FeatureExtractor(backbonePath);
        var ruleInspector = new OpenCvVisionInspector();

        var train = new List<PackageEmbeddingData>();
        var validation = new List<PackageEmbeddingData>();
        var test = new List<PackageEmbeddingData>();
        var ruleVerdicts = new List<(DefectType Truth, Verdict Verdict)>();
        var extractionTime = new Stopwatch();

        for (int classIndex = 0; classIndex < Classes.Length; classIndex++)
        {
            var (defect, type) = Classes[classIndex];
            for (int i = 0; i < ImagesPerClass; i++)
            {
                // Every image has its own seed. The seeds are independent, so
                // splitting by index is as random as shuffling, and repeatable.
                var image = Capture(defect, seed: 100_000 + (classIndex * 1_000) + i);

                extractionTime.Start();
                var sample = Embed(extractor, image, type);
                extractionTime.Stop();

                if (i < TrainPerClass)
                {
                    train.Add(sample);
                }
                else if (i < TrainPerClass + ValidationPerClass)
                {
                    validation.Add(sample);
                }
                else
                {
                    test.Add(sample);

                    // The rule-based inspection sees the same held-out images, so the two can be compared.
                    ruleVerdicts.Add((type, ruleInspector.Inspect(image, TestRecipes.Resistance100.Vision).Verdict));
                }
            }
        }

        var mlContext = new MLContext(seed: 42);
        var model = TrainHead(mlContext, train);
        var engine = mlContext.Model.CreatePredictionEngine<PackageEmbeddingData, DefectPrediction>(model);

        int[,] validationMatrix = ConfusionMatrix(engine, validation);
        var headTime = Stopwatch.StartNew();
        int[,] testMatrix = ConfusionMatrix(engine, test);
        headTime.Stop();

        double testAccuracy = Accuracy(testMatrix);

        var report = new StringBuilder();
        report.AppendLine("# Classifier training report");
        report.AppendLine();
        report.AppendLine("Written by `Full_dataset_training_meets_the_accuracy_target_and_saves_the_head`.");
        report.AppendLine("Every image is synthetic, drawn by the simulator. Nothing here was measured on real images or real hardware.");
        report.AppendLine();
        report.AppendLine(CultureInfo.InvariantCulture, $"- Images: {ImagesPerClass} per class, {Classes.Length} classes, {train.Count} training / {validation.Count} validation / {test.Count} test.");
        report.AppendLine(CultureInfo.InvariantCulture, $"- Validation accuracy: {Accuracy(validationMatrix):P2}");
        report.AppendLine(CultureInfo.InvariantCulture, $"- Test accuracy: {testAccuracy:P2} (target {AccuracyTarget:P0})");
        report.AppendLine(CultureInfo.InvariantCulture, $"- Backbone time per image, with preprocessing: {extractionTime.Elapsed.TotalMilliseconds / (Classes.Length * ImagesPerClass):F1} ms");
        report.AppendLine(CultureInfo.InvariantCulture, $"- Head time per image: {headTime.Elapsed.TotalMilliseconds / test.Count:F3} ms");
        report.AppendLine();
        report.AppendLine("## Confusion matrix on the test set (rows: truth, columns: predicted)");
        report.AppendLine();
        report.AppendLine(CultureInfo.InvariantCulture, $"| Truth | {string.Join(" | ", Classes.Select(c => c.Type))} | Class accuracy |");
        report.AppendLine(CultureInfo.InvariantCulture, $"|---|{string.Concat(Enumerable.Repeat("---|", Classes.Length + 1))}");
        for (int truth = 0; truth < Classes.Length; truth++)
        {
            var row = Enumerable.Range(0, Classes.Length).Select(predicted => testMatrix[truth, predicted]).ToArray();
            report.AppendLine(CultureInfo.InvariantCulture, $"| {Classes[truth].Type} | {string.Join(" | ", row)} | {(double)row[truth] / row.Sum():P1} |");
        }

        // PRD section 7: false call = a good unit rejected, escape = a defective unit passed.
        int good = test.Count / Classes.Length;
        int defective = test.Count - good;
        int aiFalseCalls = good - testMatrix[0, 0];
        int aiEscapes = Enumerable.Range(1, Classes.Length - 1).Sum(truth => testMatrix[truth, 0]);
        int ruleFalseCalls = ruleVerdicts.Count(r => r.Truth == DefectType.None && r.Verdict != Verdict.Pass);
        int ruleEscapes = ruleVerdicts.Count(r => r.Truth != DefectType.None && r.Verdict == Verdict.Pass);

        report.AppendLine();
        report.AppendLine("## False calls and escapes on the test set");
        report.AppendLine();
        report.AppendLine("| Inspection | False-call rate | Escape rate |");
        report.AppendLine("|---|---|---|");
        report.AppendLine(CultureInfo.InvariantCulture, $"| AI classifier | {(double)aiFalseCalls / good:P2} ({aiFalseCalls}/{good}) | {(double)aiEscapes / defective:P2} ({aiEscapes}/{defective}) |");
        report.AppendLine(CultureInfo.InvariantCulture, $"| Rule-based OpenCV | {(double)ruleFalseCalls / good:P2} ({ruleFalseCalls}/{good}) | {(double)ruleEscapes / defective:P2} ({ruleEscapes}/{defective}) |");

        // The report and the head are written before the assertion, so the head the
        // app loads is always the one the report describes, and a miss is still
        // recorded as measured.
        string modelsDirectory = Path.GetDirectoryName(backbonePath)!;
        File.WriteAllText(Path.Combine(modelsDirectory, "training-report.md"), report.ToString());
        mlContext.Model.Save(model, mlContext.Data.LoadFromEnumerable(train).Schema, Path.Combine(modelsDirectory, AiModelFiles.HeadFileName));

        Assert.True(testAccuracy >= AccuracyTarget, $"Expected at least {AccuracyTarget:P0} on the held-out test set, but measured {testAccuracy:P2}.");
    }

    private static CapturedImage Capture(UnitDefect defect, int seed)
    {
        var image = UnitImageGenerator.Generate(defect, seed);
        return new CapturedImage(image.Width, image.Height, image.Pixels);
    }

    private static PackageEmbeddingData Embed(Dinov2FeatureExtractor extractor, CapturedImage image, DefectType type) =>
        new() { Features = extractor.ExtractFeatures(image), Label = type.ToString() };

    private static ITransformer TrainHead(MLContext mlContext, IEnumerable<PackageEmbeddingData> samples)
    {
        // Multiclass logistic regression fitted with L-BFGS. It scored best on the
        // validation split (docs/TEST-PLAN.md section 4). One thread, because the
        // multi-threaded trainers gave a different model, and a different accuracy,
        // on every run.
        var options = new LbfgsMaximumEntropyMulticlassTrainer.Options
        {
            LabelColumnName = "Label",
            FeatureColumnName = "Features",
            NumberOfThreads = 1,
        };

        var pipeline = mlContext.Transforms.Conversion.MapValueToKey("Label")
            .Append(mlContext.MulticlassClassification.Trainers.LbfgsMaximumEntropy(options))
            .Append(mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

        return pipeline.Fit(mlContext.Data.LoadFromEnumerable(samples));
    }

    /// <summary>Counts predictions as [truth, predicted], both indexed by <see cref="DefectType"/>.</summary>
    private static int[,] ConfusionMatrix(
        PredictionEngine<PackageEmbeddingData, DefectPrediction> engine,
        IEnumerable<PackageEmbeddingData> samples)
    {
        var matrix = new int[Classes.Length, Classes.Length];
        foreach (var sample in samples)
        {
            var truth = Enum.Parse<DefectType>(sample.Label);
            var predicted = Enum.Parse<DefectType>(engine.Predict(sample).PredictedLabel);
            matrix[(int)truth, (int)predicted]++;
        }

        return matrix;
    }

    private static double Accuracy(int[,] matrix)
    {
        int correct = Enumerable.Range(0, Classes.Length).Sum(index => matrix[index, index]);
        return (double)correct / matrix.Cast<int>().Sum();
    }
}
