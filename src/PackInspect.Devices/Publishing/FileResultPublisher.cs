using System.IO;
using System.Text.Json;
using PackInspect.Core.Publishing;

namespace PackInspect.Devices.Publishing;

/// <summary>
/// Local implementation of IResultPublisher that drops structured JSON result payloads
/// into a configured directory tree, simulating a file-drop bridge to a factory system.
/// </summary>
public sealed class FileResultPublisher(string rootDirectory) : IResultPublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public string RootDirectory => rootDirectory;

    public async Task PublishAsync(PublishedResultPayload payload, CancellationToken cancellationToken)
    {
        string lotDirectory = Path.Combine(rootDirectory, payload.LotNumber);
        Directory.CreateDirectory(lotDirectory);

        string filePath = Path.Combine(lotDirectory, $"unit-{payload.Unit.Sequence:D6}.json");
        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, payload, JsonOptions, cancellationToken).ConfigureAwait(false);
    }
}
