namespace LegacyModernizer.Application.Contracts.Packaging
{
    public interface IPackageGenerationService
    {
        Task<GeneratedArtifact> GenerateAsync(ModernizedSolution solution,
                                              Workspace workspace,
                                              CancellationToken cancellationToken = default);
    }
}
