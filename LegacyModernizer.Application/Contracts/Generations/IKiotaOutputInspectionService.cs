namespace LegacyModernizer.Application.Contracts.Generation;

public interface IKiotaOutputInspectionService
{
    Task<KiotaClientMetadata> InspectAsync(GeneratedArtifact generatedClientArtifact,
                                           CancellationToken cancellationToken = default);
}
