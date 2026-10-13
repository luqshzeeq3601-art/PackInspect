using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using PackInspect.Core.Devices;

namespace PackInspect.Devices.Ai;

/// <summary>
/// Feature extractor backed by a frozen DINOv2 ViT-S/14 ONNX backbone.
/// Preprocesses images (224x224, ImageNet normalization) and extracts 384-dimensional embeddings.
/// </summary>
public sealed class Dinov2FeatureExtractor : IDisposable
{
    public const int EmbeddingDimension = 384;
    public const int InputSize = 224;

    private static readonly float[] Mean = [0.485f, 0.456f, 0.406f];
    private static readonly float[] Std = [0.229f, 0.224f, 0.225f];

    private readonly InferenceSession _session;

    public Dinov2FeatureExtractor(string modelPath)
    {
        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException($"DINOv2 ONNX model file not found at '{modelPath}'.", modelPath);
        }

        var sessionOptions = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
        };

        _session = new InferenceSession(modelPath, sessionOptions);
    }

    public float[] ExtractFeatures(CapturedImage image)
    {
        using var mat = Mat.FromPixelData(image.Height, image.Width, MatType.CV_8UC1, image.Pixels.ToArray());
        return ExtractFeatures(mat);
    }

    public float[] ExtractFeatures(Mat mat)
    {
        using var resized = new Mat();
        Cv2.Resize(mat, resized, new Size(InputSize, InputSize), interpolation: InterpolationFlags.Area);

        // Convert to float tensor [1, 3, 224, 224] with ImageNet normalization
        var tensor = new DenseTensor<float>([1, 3, InputSize, InputSize]);

        // If grayscale, replicate across 3 channels
        bool isColor = mat.Channels() == 3;
        using var rgbMat = isColor ? resized : null;

        for (int y = 0; y < InputSize; y++)
        {
            for (int x = 0; x < InputSize; x++)
            {
                if (isColor && rgbMat is not null)
                {
                    Vec3b bgr = rgbMat.At<Vec3b>(y, x);
                    float r = bgr.Item2 / 255.0f;
                    float g = bgr.Item1 / 255.0f;
                    float b = bgr.Item0 / 255.0f;

                    tensor[0, 0, y, x] = (r - Mean[0]) / Std[0];
                    tensor[0, 1, y, x] = (g - Mean[1]) / Std[1];
                    tensor[0, 2, y, x] = (b - Mean[2]) / Std[2];
                }
                else
                {
                    byte gray = resized.At<byte>(y, x);
                    float val = gray / 255.0f;

                    tensor[0, 0, y, x] = (val - Mean[0]) / Std[0];
                    tensor[0, 1, y, x] = (val - Mean[1]) / Std[1];
                    tensor[0, 2, y, x] = (val - Mean[2]) / Std[2];
                }
            }
        }

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("pixel_values", tensor)
        };

        using var results = _session.Run(inputs);
        var lastHiddenState = results.First(r => r.Name == "last_hidden_state").AsTensor<float>();

        // Token 0 is the [CLS] embedding token
        float[] embedding = new float[EmbeddingDimension];
        for (int i = 0; i < EmbeddingDimension; i++)
        {
            embedding[i] = lastHiddenState[0, 0, i];
        }

        return embedding;
    }

    public void Dispose() => _session.Dispose();
}
