namespace LegacyModernizer.Infrastructure.Workspace;

public sealed class WorkspacePreparationService : IWorkspacePreparationService
{
    private const string RootFolderName = "LegacyModernizer";
    private const string InputFolderName = "input";
    private const string GeneratedFolderName = "generated";
    private const string ComposedFolderName = "composed";
    private const string PackageFolderName = "package";

    public Task<Domain.Entities.Workspace> PrepareAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var workspaceId = Guid.NewGuid().ToString("N"); // formatado sem hífens e tudo minúsculo, para evitar problemas em nomes de pastas

        // Criar uma estrutura de pastas única para cada workspace, usando o ID gerado.
        // Usa o diretório temporário do sistema operacional como base.
        var rootPath = Path.Combine(Path.GetTempPath(), RootFolderName, workspaceId);
        var inputPath = Path.Combine(rootPath, InputFolderName);
        var generatedPath = Path.Combine(rootPath, GeneratedFolderName);
        var composedPath = Path.Combine(rootPath, ComposedFolderName);
        var packagePath = Path.Combine(rootPath, PackageFolderName);

        Directory.CreateDirectory(rootPath);
        Directory.CreateDirectory(inputPath);
        Directory.CreateDirectory(generatedPath);
        Directory.CreateDirectory(composedPath);
        Directory.CreateDirectory(packagePath);

        var workspacePaths = new WorkspacePaths(rootPath,
                                                inputPath,
                                                generatedPath,
                                                composedPath,
                                                packagePath);

        var workspace = new Domain.Entities.Workspace(workspacePaths);
        workspace.MarkAsPrepared();

        return Task.FromResult(workspace);
    }
}
