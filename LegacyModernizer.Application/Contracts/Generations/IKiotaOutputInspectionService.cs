namespace LegacyModernizer.Application.Contracts.Generation;

public interface IKiotaOutputInspectionService
{
    Task<KiotaClientMetadata> InspectAsync(GeneratedArtifact generatedClientArtifact,
                                           IReadOnlyCollection<ApiGroupDefinition> groups,
                                           CancellationToken cancellationToken = default);
}
