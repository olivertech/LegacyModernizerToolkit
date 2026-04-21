using LegacyModernizer.Application.Contracts.Generations.Models;

namespace LegacyModernizer.Generation.Kiota;

public sealed class ClientGenerationService : IClientGenerationService
{
    private readonly IKiotaRunner _kiotaRunner;

    public ClientGenerationService(IKiotaRunner kiotaRunner)
    {
        _kiotaRunner = kiotaRunner ?? throw new ArgumentNullException(nameof(kiotaRunner));
    }

    public async Task<GeneratedArtifact> GenerateAsync(ModernizationRequest request,
                                                       ApiSpecification specification,
                                                       Workspace workspace,
                                                       CancellationToken cancellationToken = default)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        if (specification is null)
            throw new ArgumentNullException(nameof(specification));

        if (workspace is null)
            throw new ArgumentNullException(nameof(workspace));

        if (!workspace.IsPrepared)
            throw new InvalidOperationException("Workspace must be prepared before client generation.");

        if (specification.ValidationStatus != SpecificationValidationStatus.Valid)
            throw new InvalidOperationException("Specification must be valid before client generation.");

        if (string.IsNullOrWhiteSpace(specification.LocalPath))
            throw new InvalidOperationException("Specification local path was not defined.");

        if (!File.Exists(specification.LocalPath))
            throw new FileNotFoundException("Specification file was not found.", specification.LocalPath);

        var outputPath = Path.Combine(workspace.Paths.GeneratedPath, "client");
        Directory.CreateDirectory(outputPath);

        var clientNamespace = $"{request.BaseNamespace}.ApiClient";

        var kiotaRequest = new KiotaGenerationRequest
        {
            SpecificationPath = specification.LocalPath,
            OutputPath = outputPath,
            ClientNamespace = clientNamespace,
            Language = "csharp"
        };

        var result = await _kiotaRunner.ExecuteAsync(kiotaRequest, cancellationToken);

        if (!result.Success)
        {
            var message = string.IsNullOrWhiteSpace(result.StandardError)
                ? "Kiota client generation failed."
                : $"Kiota client generation failed: {result.StandardError}";

            throw new InvalidOperationException(message);
        }

        if (!Directory.Exists(outputPath))
            throw new InvalidOperationException("Client generation output directory was not created.");

        if (!Directory.EnumerateFileSystemEntries(outputPath).Any())
            throw new InvalidOperationException("Client generation completed, but no files were generated.");

        return new GeneratedArtifact(
            ArtifactType.GeneratedClient,
            new ArtifactLocation(outputPath),
            ExecutionStep.ClientGeneration);
    }
}
