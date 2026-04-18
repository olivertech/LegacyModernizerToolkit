namespace LegacyModernizer.Domain.Entities;

/// <summary>
/// Um artefato gerado em uma etapa do processo.
/// É uma entidade do domínio porque representa um resultado concreto e significativo do processo de modernização.
/// Ela é uma boa entidade de apoio porque permite que a execução vá acumulando evidências do que foi produzido.
/// </summary>
public sealed class GeneratedArtifact
{
    public Guid Id { get; private set; }
    public ArtifactType Type { get; private set; }
    public ArtifactLocation Location { get; private set; }
    public ExecutionStep CreatedAtStep { get; private set; }
    public float SizeInBytes { get; set; }

    public DateTime CreatedAt { get; private set; }

    public GeneratedArtifact(ArtifactType type,
                             ArtifactLocation location,
                             ExecutionStep createdAtStep)
    {
        Location = location ?? throw new ArgumentNullException(nameof(location));

        Id = Guid.NewGuid();
        Type = type;
        CreatedAtStep = createdAtStep;
        CreatedAt = DateTime.UtcNow;
        SizeInBytes = sizeof(float);
    }

    public void Relocate(ArtifactLocation newLocation)
    {
        Location = newLocation ?? throw new ArgumentNullException(nameof(newLocation));
    }
}
