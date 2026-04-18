namespace LegacyModernizer.Domain.Entities;

/// <summary>
/// O ambiente temporário de execução.
/// Ainda que seja técnico, ele participa do domínio do processo de modernização.
/// </summary>
public sealed class Workspace
{
    public Guid Id { get; private set; }
    public WorkspacePaths Paths { get; private set; }
    public bool IsPrepared { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? PreparedAt { get; private set; }

    public Workspace(WorkspacePaths paths)
    {
        Paths = paths ?? throw new ArgumentNullException(nameof(paths));
        Id = Guid.NewGuid();
        IsPrepared = false;
        CreatedAt = DateTime.UtcNow;
    }

    public void MarkAsPrepared()
    {
        IsPrepared = true;
        PreparedAt = DateTime.UtcNow;
    }

    public void UpdatePaths(WorkspacePaths paths)
    {
        Paths = paths ?? throw new ArgumentNullException(nameof(paths));
        UpdatedAt = DateTime.UtcNow;
    }
}
