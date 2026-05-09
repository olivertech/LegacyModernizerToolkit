namespace LegacyModernizer.Infrastructure.Workspace;

/// <summary>
/// Cria a estrutura temporária isolada usada por uma execução completa de modernização.
/// </summary>
public sealed class WorkspacePreparationService : IWorkspacePreparationService
{
    private const string RootFolderName = "LegacyModernizer";
    private const string InputFolderName = "input";
    private const string GeneratedFolderName = "generated";
    private const string ComposedFolderName = "composed";
    private const string PackageFolderName = "package";

    /// <summary>
    /// Prepara um workspace novo para evitar interferência entre execuções e facilitar rastreabilidade.
    /// </summary>
    public Task<Domain.Entities.Workspace> PrepareAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Usamos um GUID compacto para garantir unicidade e evitar problemas com nomes de pasta.
        var workspaceId = Guid.NewGuid().ToString("N");

        // Cada execução recebe seu próprio diretório raiz para isolar spec, client gerado,
        // solução composta e pacote final dentro do mesmo contexto físico.
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
