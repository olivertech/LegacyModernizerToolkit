namespace LegacyModernizer.Infrastructure.Packaging;

public sealed class PackageGenerationService : IPackageGenerationService
{
    public async Task<GeneratedArtifact> GenerateAsync(ModernizedSolution solution,
                                                       Domain.Entities.Workspace workspace,
                                                       CancellationToken cancellationToken = default)
    {
        if (solution is null)
            throw new ArgumentNullException(nameof(solution));

        if (workspace is null)
            throw new ArgumentNullException(nameof(workspace));

        if (!workspace.IsPrepared)
            throw new InvalidOperationException("Workspace must be prepared before package generation.");

        if (string.IsNullOrWhiteSpace(solution.RootPath))
            throw new InvalidOperationException("Solution root path was not defined.");

        if (!Directory.Exists(solution.RootPath))
            throw new DirectoryNotFoundException($"Solution root directory was not found: {solution.RootPath}");

        Directory.CreateDirectory(workspace.Paths.PackagePath);

        var packageFileName = $"{solution.ProjectName}.zip";
        var packageFullPath = Path.Combine(workspace.Paths.PackagePath, packageFileName);

        if (File.Exists(packageFullPath))
        {
            File.Delete(packageFullPath);
        }

        cancellationToken.ThrowIfCancellationRequested();

        /*
         * Cria um arquivo ZIP a partir do diretório da solução, incluindo o diretório base.
         * Gera uma estrutura de pastas conforme abaixo, dentro do arquivo ZIP:
         * MeuProjeto/
              MeuProjeto.sln
              src/
              README.md
        */
        await Task.Run(() =>
        {
            ZipFile.CreateFromDirectory(
                solution.RootPath,
                packageFullPath,
                CompressionLevel.Optimal,
                includeBaseDirectory: true);
        }, cancellationToken);

        if (!File.Exists(packageFullPath))
            throw new InvalidOperationException("Package file was not generated.");

        return new GeneratedArtifact(ArtifactType.Package, new ArtifactLocation(packageFullPath), ExecutionStep.Packaging);
    }
}
