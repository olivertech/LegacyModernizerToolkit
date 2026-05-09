namespace LegacyModernizer.Domain.ValueObjects;

/// <summary>
/// O pedido feito pelo usuário para gerar a solução modernizada.
/// </summary>
public sealed record ModernizationRequest
{
    /// <summary>
    /// Fonte da specification OpenAPI usada como contrato de entrada.
    /// </summary>
    public SpecificationSource SpecificationSource { get; }

    /// <summary>
    /// Nome lógico da solução gerada.
    /// </summary>
    public ProjectName ProjectName { get; }

    /// <summary>
    /// Namespace base pedido para a saída.
    /// </summary>
    public NamespaceName BaseNamespace { get; }

    /// <summary>
    /// Framework alvo da solução gerada.
    /// </summary>
    public string? TargetFramework { get; }

    /// <summary>
    /// Define se a saída será autônoma ou incorporável.
    /// </summary>
    public GenerationMode GenerationMode { get; }

    /// <summary>
    /// Define se o token será explícito por método ou resolvido pelo host.
    /// </summary>
    public AuthenticationMode AuthenticationMode { get; }

    /// <summary>
    /// Prefixo obrigatório no modo Embedded para evitar colisão com a solution hospedeira.
    /// </summary>
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

        // No modo Embedded a geração precisa de um prefixo estável para compor
        // nomes como {Prefix}.Lmt.Application.* sem colidir com o projeto legado.
        if (GenerationMode == GenerationMode.Embedded && EmbeddedProjectPrefix is null)
        {
            throw new ArgumentException(
                "Embedded project prefix is required when generation mode is Embedded.",
                nameof(embeddedProjectPrefix));
        }
    }
}
