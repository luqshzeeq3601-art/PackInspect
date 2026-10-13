using Microsoft.ML.Data;

namespace PackInspect.Devices.Ai;

public sealed class PackageEmbeddingData
{
    [VectorType(384)]
    public float[] Features { get; set; } = null!;

    public string Label { get; set; } = null!;
}

public sealed class DefectPrediction
{
    [ColumnName("PredictedLabel")]
    public string PredictedLabel { get; set; } = null!;

    public float[] Score { get; set; } = null!;
}
