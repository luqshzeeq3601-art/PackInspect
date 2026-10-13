using Microsoft.ML.OnnxRuntime;
using PackInspect.Tests.Fakes;

namespace PackInspect.Tests.Unit;

public class Dinov2ModelVerificationTests
{
    [Fact]
    public void Model_loads_and_exposes_expected_input_and_output_tensors()
    {
        using var session = new InferenceSession(AiModelFiles.BackboneOrSkip());

        // Verify input metadata
        Assert.NotEmpty(session.InputMetadata);
        var inputMeta = session.InputMetadata.First();
        Assert.Equal("pixel_values", inputMeta.Key);

        // Verify output metadata
        Assert.NotEmpty(session.OutputMetadata);
        Assert.Contains("last_hidden_state", session.OutputMetadata.Keys);

        // Run a dummy forward pass to verify inference execution and embedding shape
        var inputTensor = new Microsoft.ML.OnnxRuntime.Tensors.DenseTensor<float>([1, 3, 224, 224]);
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("pixel_values", inputTensor)
        };

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        using var results = session.Run(inputs);
        stopwatch.Stop();

        var output = results.First(r => r.Name == "last_hidden_state").AsTensor<float>();
        // Shape is [1, sequence_length, 384]
        Assert.Equal(3, output.Dimensions.Length);
        Assert.Equal(1, output.Dimensions[0]);
        Assert.Equal(384, output.Dimensions[2]); // 384-dimensional DINOv2 ViT-S embedding
    }
}
