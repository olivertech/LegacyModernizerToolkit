namespace LegacyModernizer.Infrastructure.Workspace;

public sealed class WorkspacePreparationService : IWorkspacePreparationService
{
    public Task<Domain.Entities.Workspace> PrepareAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var workspaceId = Guid.NewGuid().ToString("N"); // formatado sem hífens e tudo minúsculo, para evitar problemas em nomes de pastas
        var rootPath = Path.Combine(Path.GetTempPath(), "LegacyModernizer", workspaceId);

        var inputPath = Path.Combine(rootPath, "input");
        var generatedPath = Path.Combine(rootPath, "generated");
        var composedPath = Path.Combine(rootPath, "composed");
        var packagePath = Path.Combine(rootPath, "package");

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
