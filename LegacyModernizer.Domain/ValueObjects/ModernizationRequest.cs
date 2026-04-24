namespace LegacyModernizer.Domain.ValueObjects;

/// <summary>
/// O pedido feito pelo usuário para gerar a solução modernizada.
/// </summary>
public sealed record ModernizationRequest
{
    public SpecificationSource SpecificationSource { get; }
    public ProjectName ProjectName { get; }
    public NamespaceName BaseNamespace { get; }
    public string? TargetFramework { get; }

    public ModernizationRequest(SpecificationSource specificationSource,
                                ProjectName projectName,
                                NamespaceName baseNamespace,
                                string targetFramework)
    {
        SpecificationSource = specificationSource ?? throw new ArgumentNullException(nameof(specificationSource));
        ProjectName = projectName ?? throw new ArgumentNullException(nameof(projectName));
        BaseNamespace = baseNamespace ?? throw new ArgumentNullException(nameof(baseNamespace));

        if (string.IsNullOrWhiteSpace(targetFramework))
            throw new ArgumentException("Target framework cannot be empty.", nameof(targetFramework));

        TargetFramework = targetFramework.Trim();
    }
}
