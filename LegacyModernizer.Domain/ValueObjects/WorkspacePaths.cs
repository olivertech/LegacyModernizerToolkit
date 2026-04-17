namespace LegacyModernizer.Domain.ValueObjects;

/// <summary>
/// Agrupa os caminhos relevantes do workspace, e
/// carrega regra para formato válido
/// de caminhos em C#.
/// Esse VO agrupa os diretórios do workspace.
/// Ele não deve aceitar caminhos vazios.
/// </summary>
public sealed record WorkspacePaths
{
    public string RootPath { get; }
    public string InputPath { get; }
    public string GeneratedPath { get; }
    public string ComposedPath { get; }
    public string PackagePath { get; }

    public WorkspacePaths(string rootPath,
                          string inputPath,
                          string generatedPath,
                          string composedPath,
                          string packagePath)
    {
        RootPath = ValidatePath(rootPath, nameof(rootPath));
        InputPath = ValidatePath(inputPath, nameof(inputPath));
        GeneratedPath = ValidatePath(generatedPath, nameof(generatedPath));
        ComposedPath = ValidatePath(composedPath, nameof(composedPath));
        PackagePath = ValidatePath(packagePath, nameof(packagePath));
    }

    private static string ValidatePath(string path, string paramName)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be null or empty.", paramName);

        return path.Trim();
    }
}
