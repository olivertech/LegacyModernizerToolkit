namespace LegacyModernizer.Domain.ValueObjects;

/// <summary>
/// Representa onde um artefato está.
/// </summary>
public sealed record ArtifactLocation
{
    public string FullPath { get; }
    public string FileName { get; }

    public ArtifactLocation(string fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
            throw new ArgumentException("Artifact path cannot be null or empty.", nameof(fullPath));

        fullPath = fullPath.Trim();

        var fileName = Path.GetFileName(fullPath);

        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("Artifact path must contain a valid file or folder name.", nameof(fullPath));

        FullPath = fullPath;
        FileName = fileName;
    }

    /// <summary>
    /// Esse override é útil para facilitar a depuração, logs, 
    /// mensagens, debug, concatenação de string, geração de código e templates,
    /// expondo o valor "real" de forma limpa.
    /// </summary>
    /// <returns></returns>
    public override string ToString() => FullPath;
}
