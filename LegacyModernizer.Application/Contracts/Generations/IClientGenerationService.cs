namespace LegacyModernizer.Application.Contracts.Generations;

public interface IClientGenerationService
{
    Task<GeneratedArtifact> GenerateAsync(ModernizationRequest request,
                                          ApiSpecification specification,
                                          Workspace workspace,
                                          CancellationToken cancellationToken = default);
}
