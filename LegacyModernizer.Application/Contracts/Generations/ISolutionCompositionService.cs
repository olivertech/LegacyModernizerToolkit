namespace LegacyModernizer.Application.Contracts.Generations;

public interface ISolutionCompositionService
{
    Task<ModernizedSolution> ComposeAsync(ModernizationRequest request,
                                          Workspace workspace,
                                          GeneratedArtifact generatedClientArtifact,
                                          IReadOnlyCollection<ApiGroupDefinition> groups,
                                          KiotaClientMetadata kiotaMetadata,
                                          CancellationToken cancellationToken = default);
}
