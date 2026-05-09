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
    public GenerationMode GenerationMode { get; }
    public AuthenticationMode AuthenticationMode { get; }
    public EmbeddedProjectPrefix? EmbeddedProjectPrefix { get; }

    public ModernizationRequest(SpecificationSource specificationSource,
                                ProjectName projectName,
                                NamespaceName baseNamespace,
                                string targetFramework,
                                GenerationMode generationMode = GenerationMode.Standalone,
                                AuthenticationMode authenticationMode = AuthenticationMode.PerMethodToken,
                                EmbeddedProjectPrefix? embeddedProjectPrefix = null)
    {
        SpecificationSource = specificationSource ?? throw new ArgumentNullException(nameof(specificationSource));
        ProjectName = projectName ?? throw new ArgumentNullException(nameof(projectName));
        BaseNamespace = baseNamespace ?? throw new ArgumentNullException(nameof(baseNamespace));

        if (string.IsNullOrWhiteSpace(targetFramework))
            throw new ArgumentException("Target framework cannot be empty.", nameof(targetFramework));

        TargetFramework = targetFramework.Trim();
        GenerationMode = generationMode;
        AuthenticationMode = authenticationMode;
        EmbeddedProjectPrefix = embeddedProjectPrefix;

        if (GenerationMode == GenerationMode.Embedded && EmbeddedProjectPrefix is null)
        {
            throw new ArgumentException(
                "Embedded project prefix is required when generation mode is Embedded.",
                nameof(embeddedProjectPrefix));
        }
    }
}
