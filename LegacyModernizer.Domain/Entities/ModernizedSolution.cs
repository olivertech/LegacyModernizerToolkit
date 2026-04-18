namespace LegacyModernizer.Domain.Entities;

/// <summary>
/// Representa a solução final estruturada antes do empacotamento.
/// É ela quem tem a responsabilidade de organizar os arquivos e diretórios que compõem a solução modernizada.
/// </summary>
public sealed class ModernizedSolution
{
    public Guid Id { get; private set; }
    public ProjectName ProjectName { get; private set; }
    public NamespaceName BaseNamespace { get; private set; }
    public string RootPath { get; private set; }
    public string SolutionFilePath { get; private set; }
    public bool IsPackaged { get; private set; }

    public ModernizedSolution(ProjectName projectName,
                              NamespaceName baseNamespace,
                              string rootPath,
                              string solutionFilePath)
    {
        ProjectName = projectName ?? throw new ArgumentNullException(nameof(projectName));
        BaseNamespace = baseNamespace ?? throw new ArgumentNullException(nameof(baseNamespace));
        RootPath = ValidatePath(rootPath, nameof(rootPath));
        SolutionFilePath = ValidatePath(solutionFilePath, nameof(solutionFilePath));

        Id = Guid.NewGuid();
        IsPackaged = false;
    }

    public void MarkAsPackaged()
    {
        IsPackaged = true;
    }

    public void UpdateRootPath(string rootPath)
    {
        RootPath = ValidatePath(rootPath, nameof(rootPath));
    }

    public void UpdateSolutionFilePath(string solutionFilePath)
    {
        SolutionFilePath = ValidatePath(solutionFilePath, nameof(solutionFilePath));
    }

    private static string ValidatePath(string path, string paramName)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be null or empty.", paramName);

        return path.Trim();
    }
}
