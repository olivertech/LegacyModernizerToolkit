namespace LegacyModernizer.Generation.Composition;

/// <summary>
/// Resolve a convenção de nomes da solução gerada conforme o modo escolhido pelo usuário.
/// </summary>
internal static class ProjectNamingStrategyResolver
{
    /// <summary>
    /// Decide entre a convenção Standalone e Embedded e devolve o layout completo de projetos e namespaces.
    /// </summary>
    public static SolutionProjectLayout Resolve(ModernizationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.GenerationMode switch
        {
            GenerationMode.Embedded => ResolveEmbedded(request),
            _ => ResolveStandalone(request)
        };
    }

    private static SolutionProjectLayout ResolveStandalone(ModernizationRequest request)
    {
        var projectName = request.ProjectName.ToString();
        var baseNamespace = request.BaseNamespace.ToString();

        return new SolutionProjectLayout(
            SolutionName: projectName,
            SolutionRootFolderName: projectName,
            ApiClientProjectName: $"{projectName}.ApiClient",
            ContractsProjectName: $"{projectName}.Core",
            HttpProjectName: $"{projectName}.Infrastructure",
            ApiClientNamespace: $"{baseNamespace}.ApiClient",
            ContractsNamespace: $"{baseNamespace}.Core",
            HttpNamespace: $"{baseNamespace}.Infrastructure",
            ClientClassName: $"{projectName}ApiClient");
    }

    private static SolutionProjectLayout ResolveEmbedded(ModernizationRequest request)
    {
        // O prefixo é a peça que isola o módulo gerado dentro da solution legado.
        var prefix = request.EmbeddedProjectPrefix?.ToString()
                     ?? throw new InvalidOperationException("Embedded project prefix is required for Embedded mode.");

        var applicationRoot = $"{prefix}.Lmt.Application";

        return new SolutionProjectLayout(
            SolutionName: request.ProjectName.ToString(),
            SolutionRootFolderName: request.ProjectName.ToString(),
            ApiClientProjectName: $"{applicationRoot}.ApiClient",
            ContractsProjectName: $"{applicationRoot}.Contracts",
            HttpProjectName: $"{applicationRoot}.Http",
            ApiClientNamespace: $"{applicationRoot}.ApiClient",
            ContractsNamespace: $"{applicationRoot}.Contracts",
            HttpNamespace: $"{applicationRoot}.Http",
            ClientClassName: NormalizeIdentifier($"{applicationRoot}.ApiClient"));
    }

    private static string NormalizeIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        // O nome final da classe raiz do client precisa ser um identificador C# válido,
        // mesmo quando nasce de uma composição de namespace no modo Embedded.
        var parts = value
            .Split(new[] { '-', '_', '.', '/', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => new string(part.Where(char.IsLetterOrDigit).ToArray()))
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToArray();

        if (parts.Length == 0)
            return string.Empty;

        return string.Concat(parts.Select(part =>
            part.Length == 1
                ? part.ToUpperInvariant()
                : char.ToUpperInvariant(part[0]) + part[1..]));
    }
}
