namespace LegacyModernizer.Application.Contracts.Generations;

public interface IClientGenerationService
{
    Task<GeneratedArtifact> GenerateAsync(ApiSpecification specification,
                                          Workspace workspace,
                                          CancellationToken cancellationToken = default);
}
