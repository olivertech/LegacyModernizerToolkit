namespace LegacyModernizer.Generation.Composition;

internal static class ProjectNamingStrategyResolver
{
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
