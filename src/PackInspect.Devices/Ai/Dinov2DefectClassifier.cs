using Microsoft.ML;
using PackInspect.Core.Ai;
using PackInspect.Core.Devices;
using PackInspect.Core.Domain;

namespace PackInspect.Devices.Ai;

/// <summary>
/// AI defect classifier combining a frozen DINOv2 feature extractor with a trained ML.NET classifier head.
/// </summary>
public sealed class Dinov2DefectClassifier : IDefectClassifier, IDisposable
{
    public const string DefaultModelVersion = "DINOv2-ViT-S-MLNET-v1";

    private readonly Dinov2FeatureExtractor _extractor;
    private readonly MLContext _mlContext;
    private readonly PredictionEngine<PackageEmbeddingData, DefectPrediction>? _engine;
    private readonly string _modelVersion;

    public Dinov2DefectClassifier(
        string backboneOnnxPath,
        string? headModelPath = null,
        string modelVersion = DefaultModelVersion)
    {
        _extractor = new Dinov2FeatureExtractor(backboneOnnxPath);
        _mlContext = new MLContext(seed: 42);
        _modelVersion = modelVersion;

        if (!string.IsNullOrEmpty(headModelPath) && File.Exists(headModelPath))
        {
            var model = _mlContext.Model.Load(headModelPath, out _);
            _engine = _mlContext.Model.CreatePredictionEngine<PackageEmbeddingData, DefectPrediction>(model);
        }
    }

    public Dinov2DefectClassifier(Dinov2FeatureExtractor extractor, ITransformer model, string modelVersion = DefaultModelVersion)
    {
        _extractor = extractor;
        _mlContext = new MLContext(seed: 42);
        _modelVersion = modelVersion;
        _engine = _mlContext.Model.CreatePredictionEngine<PackageEmbeddingData, DefectPrediction>(model);
    }

    public Task<DefectClassification> ClassifyAsync(CapturedImage image, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        float[] features = _extractor.ExtractFeatures(image);

        if (_engine is null)
        {
            // Fallback if no head is trained: classify as None with 100% confidence
            var fallbackDict = new Dictionary<DefectType, double> { [DefectType.None] = 1.0 };
            return Task.FromResult(new DefectClassification(DefectType.None, 1.0, fallbackDict, _modelVersion));
        }

        var input = new PackageEmbeddingData { Features = features, Label = string.Empty };
        DefectPrediction prediction;
        lock (_engine)
        {
            prediction = _engine.Predict(input);
        }

        if (!Enum.TryParse<DefectType>(prediction.PredictedLabel, out var label))
        {
            label = DefectType.None;
        }

        // In ML.NET multiclass MaximumEntropy, Score contains the calibrated probabilities for each class
        var probabilities = new Dictionary<DefectType, double>();
        double confidence = 0.95;
        if (prediction.Score is { Length: > 0 })
        {
            float maxScore = prediction.Score.Max();
            confidence = Math.Round((double)maxScore, 4);
        }

        probabilities[label] = confidence;

        return Task.FromResult(new DefectClassification(label, confidence, probabilities, _modelVersion));
    }

    public void Dispose() => _extractor.Dispose();
}
