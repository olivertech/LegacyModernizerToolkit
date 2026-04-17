namespace LegacyModernizer.Domain.ValueObjects;

/// <summary>
/// O pedido feito pelo usuário para gerar a solução modernizada.
/// </summary>
public sealed record ModernizationRequest
{
    public SpecificationSource SpecificationSource { get; }
    public ProjectName ProjectName { get; }
    public NamespaceName BaseNamespace { get; }

    public ModernizationRequest(SpecificationSource specificationSource,
                                ProjectName projectName,
                                NamespaceName baseNamespace)
    {
        SpecificationSource = specificationSource
            ?? throw new ArgumentNullException(nameof(specificationSource));

        ProjectName = projectName
            ?? throw new ArgumentNullException(nameof(projectName));

        BaseNamespace = baseNamespace
            ?? throw new ArgumentNullException(nameof(baseNamespace));
    }
}
