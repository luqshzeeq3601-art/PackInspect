namespace PackInspect.Core.Publishing;

/// <summary>
/// Seam for publishing per-unit results to a factory system or file drop.
/// </summary>
public interface IResultPublisher
{
    Task PublishAsync(PublishedResultPayload payload, CancellationToken cancellationToken);
}
