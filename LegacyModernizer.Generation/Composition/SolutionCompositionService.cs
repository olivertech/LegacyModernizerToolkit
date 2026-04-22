namespace LegacyModernizer.Generation.Composition;

public sealed class SolutionCompositionService : ISolutionCompositionService
{
    /*
     * Saída esperada da composição:
     * 
     *{ProjectName}/
     * {ProjectName}.sln
     * README.md
     * src/
     *   {ProjectName}.ApiClient/
     *     ...arquivos Kiota...
     *   {ProjectName}.Core/
     *     Interfaces/
     *       IApiFacade.cs
     *       IAuthService.cs
     *       IHomeService.cs
     *       IProfileService.cs 
     *       ...
     *     Models/
     *       ApiOptions.cs
     *   {ProjectName}.Infrastructure/
     *     Services/
     *       AuthService.cs
     *       HomeService.cs
     *       ProfileService.cs 
     *       ...
     *     Facades/
     *       ApiFacade.cs
     *     DependencyInjection/
     *       ServiceCollectionExtensions.cs
     * 
     * O serviço de composição é responsável por criar a estrutura de pastas e arquivos necessários para a solução modernizada.
     * Ele deve validar os inputs, garantir que o workspace esteja preparado, e organizar os artefatos gerados de forma coerente.
     * A solução resultante deve ser fácil de entender e navegar, mesmo para desenvolvedores que não estão familiarizados com o processo de modernização.
     */
    public Task<ModernizedSolution> ComposeAsync(ModernizationRequest request,
                                                 Workspace workspace,
                                                 GeneratedArtifact generatedClientArtifact,
                                                 IReadOnlyCollection<ApiGroupDefinition> groups,
                                                 CancellationToken cancellationToken = default)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        if (workspace is null)
            throw new ArgumentNullException(nameof(workspace));

        if (generatedClientArtifact is null)
            throw new ArgumentNullException(nameof(generatedClientArtifact));

        if (groups is null)
            throw new ArgumentNullException(nameof(groups));

        if (!workspace.IsPrepared)
            throw new InvalidOperationException("Workspace must be prepared before solution composition.");

        if (generatedClientArtifact.Type != ArtifactType.GeneratedClient)
            throw new InvalidOperationException("The provided artifact is not a generated client artifact.");

        var generatedClientPath = generatedClientArtifact.Location.FullPath;

        if (!Directory.Exists(generatedClientPath))
            throw new DirectoryNotFoundException($"Generated client directory was not found: {generatedClientPath}");

        var projectName = request.ProjectName.ToString();
        var baseNamespace = request.BaseNamespace.ToString();

        var normalizedGroups = groups
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .Select(x => x.Name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToArray();

        var solutionRootPath = Path.Combine(workspace.Paths.ComposedPath, projectName);
        var srcPath = Path.Combine(solutionRootPath, "src");

        var apiClientProjectPath = Path.Combine(srcPath, $"{projectName}.ApiClient");
        var coreProjectPath = Path.Combine(srcPath, $"{projectName}.Core");
        var infrastructureProjectPath = Path.Combine(srcPath, $"{projectName}.Infrastructure");

        var coreInterfacesPath = Path.Combine(coreProjectPath, "Interfaces");
        var coreModelsPath = Path.Combine(coreProjectPath, "Models");

        var infrastructureServicesPath = Path.Combine(infrastructureProjectPath, "Services");
        var infrastructureFacadesPath = Path.Combine(infrastructureProjectPath, "Facades");
        var infrastructureDependencyInjectionPath = Path.Combine(infrastructureProjectPath, "DependencyInjection");

        Directory.CreateDirectory(solutionRootPath);
        Directory.CreateDirectory(srcPath);

        Directory.CreateDirectory(coreProjectPath);
        Directory.CreateDirectory(coreInterfacesPath);
        Directory.CreateDirectory(coreModelsPath);

        Directory.CreateDirectory(infrastructureProjectPath);
        Directory.CreateDirectory(infrastructureServicesPath);
        Directory.CreateDirectory(infrastructureFacadesPath);
        Directory.CreateDirectory(infrastructureDependencyInjectionPath);

        CopyDirectory(generatedClientPath, apiClientProjectPath);

        var solutionFilePath = Path.Combine(solutionRootPath, $"{projectName}.sln");
        CreatePlaceholderSolutionFile(solutionFilePath, projectName);

        CreateApiOptionsFile(coreModelsPath, baseNamespace);

        foreach (var groupName in normalizedGroups)
        {
            CreateGroupInterfaceFile(coreInterfacesPath, baseNamespace, groupName);
            CreateGroupServiceFile(infrastructureServicesPath, baseNamespace, groupName);
        }

        CreateApiFacadeInterfaceFile(coreInterfacesPath, baseNamespace, normalizedGroups);
        CreateApiFacadeFile(infrastructureFacadesPath, baseNamespace, normalizedGroups);
        CreateServiceCollectionExtensionsFile(infrastructureDependencyInjectionPath, baseNamespace, normalizedGroups);
        CreateReadmeFile(solutionRootPath, request, normalizedGroups);

        var solution = new ModernizedSolution(
            request.ProjectName,
            request.BaseNamespace,
            solutionRootPath,
            solutionFilePath);

        return Task.FromResult(solution);
    }

    private static void CopyDirectory(string sourcePath, string destinationPath)
    {
        var sourceDirectory = new DirectoryInfo(sourcePath);

        if (!sourceDirectory.Exists)
            throw new DirectoryNotFoundException($"Source directory was not found: {sourcePath}");

        Directory.CreateDirectory(destinationPath);

        foreach (var file in sourceDirectory.GetFiles("*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourcePath, file.FullName);
            var destinationFilePath = Path.Combine(destinationPath, relativePath);

            var destinationDirectory = Path.GetDirectoryName(destinationFilePath);
            if (!string.IsNullOrWhiteSpace(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            file.CopyTo(destinationFilePath, overwrite: true);
        }
    }

    private static void CreatePlaceholderSolutionFile(string solutionFilePath, string projectName)
    {
        var content =
$$"""
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
# Placeholder solution generated by Legacy Modernizer Toolkit
# Project: {{projectName}}
# This structure is ready to evolve into a compilable multi-project solution.
""";

        File.WriteAllText(solutionFilePath, content);
    }

    private static void CreateApiOptionsFile(string coreModelsPath, string baseNamespace)
    {
        var filePath = Path.Combine(coreModelsPath, "ApiOptions.cs");

        var content =
$$"""
namespace {{baseNamespace}}.Core.Models;

public sealed class ApiOptions
{
    public string BaseUrl { get; set; } = string.Empty;
}
""";

        File.WriteAllText(filePath, content);
    }

    private static void CreateGroupInterfaceFile(string coreInterfacesPath, string baseNamespace, string groupName)
    {
        var filePath = Path.Combine(coreInterfacesPath, $"I{groupName}Service.cs");

        var content =
$$"""
namespace {{baseNamespace}}.Core.Interfaces;

public interface I{{groupName}}Service
{
    string GroupName { get; }
}
""";

        File.WriteAllText(filePath, content);
    }

    private static void CreateGroupServiceFile(string infrastructureServicesPath, string baseNamespace, string groupName)
    {
        var filePath = Path.Combine(infrastructureServicesPath, $"{groupName}Service.cs");

        var content =
$$"""
using {{baseNamespace}}.Core.Interfaces;

namespace {{baseNamespace}}.Infrastructure.Services;

public sealed class {{groupName}}Service : I{{groupName}}Service
{
    public string GroupName => "{{groupName}}";
}
""";

        File.WriteAllText(filePath, content);
    }

    private static void CreateApiFacadeInterfaceFile(
        string coreInterfacesPath,
        string baseNamespace,
        IReadOnlyCollection<string> groups)
    {
        var filePath = Path.Combine(coreInterfacesPath, "IApiFacade.cs");

        var properties = groups.Count == 0
            ? "    // No API groups were detected."
            : string.Join(Environment.NewLine, groups.Select(g => $"    I{g}Service {g} {{ get; }}"));

        var content =
$$"""
namespace {{baseNamespace}}.Core.Interfaces;

public interface IApiFacade
{
{{properties}}
}
""";

        File.WriteAllText(filePath, content);
    }

    private static void CreateApiFacadeFile(
        string infrastructureFacadesPath,
        string baseNamespace,
        IReadOnlyCollection<string> groups)
    {
        var filePath = Path.Combine(infrastructureFacadesPath, "ApiFacade.cs");

        var usingBlock = $"{baseNamespace}.Core.Interfaces";

        var constructorParameters = groups.Count == 0
            ? string.Empty
            : string.Join("," + Environment.NewLine, groups.Select(g => $"        I{g}Service {ToCamelCase(g)}"));

        var assignments = groups.Count == 0
            ? "        // No API groups were detected."
            : string.Join(Environment.NewLine, groups.Select(g => $"        {g} = {ToCamelCase(g)} ?? throw new ArgumentNullException(nameof({ToCamelCase(g)}));"));

        var properties = groups.Count == 0
            ? "    // No API groups were detected."
            : string.Join(Environment.NewLine + Environment.NewLine, groups.Select(g => $"    public I{g}Service {g} {{ get; }}"));

        var constructorBlock = groups.Count == 0
            ? """
    public ApiFacade()
    {
        // No API groups were detected.
    }
"""
            :
$$"""
    public ApiFacade(
{{constructorParameters}})
    {
{{assignments}}
    }
""";

        var content =
$$"""
using {{usingBlock}};

namespace {{baseNamespace}}.Infrastructure.Facades;

public sealed class ApiFacade : IApiFacade
{
{{constructorBlock}}

{{properties}}
}
""";

        File.WriteAllText(filePath, content);
    }

    private static void CreateServiceCollectionExtensionsFile(
        string infrastructureDependencyInjectionPath,
        string baseNamespace,
        IReadOnlyCollection<string> groups)
    {
        var filePath = Path.Combine(infrastructureDependencyInjectionPath, "ServiceCollectionExtensions.cs");

        var interfaceUsings = $"{baseNamespace}.Core.Interfaces";
        var modelUsings = $"{baseNamespace}.Core.Models";
        var facadeUsings = $"{baseNamespace}.Infrastructure.Facades";
        var serviceUsings = $"{baseNamespace}.Infrastructure.Services";

        var registrations = groups.Count == 0
            ? "        // No API groups were detected."
            : string.Join(Environment.NewLine, groups.Select(g => $"        services.AddScoped<I{g}Service, {g}Service>();"));

        var content =
$$"""
using {{interfaceUsings}};
using {{modelUsings}};
using {{facadeUsings}};
using {{serviceUsings}};
using Microsoft.Extensions.DependencyInjection;

namespace {{baseNamespace}}.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGeneratedApi(this IServiceCollection services, Action<ApiOptions> configure)
    {
        if (services is null)
            throw new ArgumentNullException(nameof(services));

        if (configure is null)
            throw new ArgumentNullException(nameof(configure));

        var options = new ApiOptions();
        configure(options);

        services.AddSingleton(options);

{{registrations}}
        services.AddScoped<IApiFacade, ApiFacade>();

        return services;
    }
}
""";

        File.WriteAllText(filePath, content);
    }

    private static void CreateReadmeFile(
        string solutionRootPath,
        ModernizationRequest request,
        IReadOnlyCollection<string> groups)
    {
        var projectName = request.ProjectName.ToString();
        var baseNamespace = request.BaseNamespace.ToString();

        var groupsSection = groups.Count == 0
            ? "- No API groups were detected during composition."
            : string.Join(Environment.NewLine, groups.Select(g => $"- `{g}`"));

        var serviceFilesSection = groups.Count == 0
            ? "- No service files were generated."
            : string.Join(Environment.NewLine, groups.Select(g => $"- `Services/{g}Service.cs`"));

        var interfaceFilesSection = groups.Count == 0
            ? "- No interface files were generated."
            : string.Join(Environment.NewLine, groups.Select(g => $"- `Interfaces/I{g}Service.cs`"));

        var filePath = Path.Combine(solutionRootPath, "README.md");

        var content =
$$"""
# {{projectName}}

This solution was generated by Legacy Modernizer Toolkit.

## Purpose

This output represents a first modernization layer over an OpenAPI/Swagger specification, combining:

- a generated API client
- service contracts grouped by API domain
- service implementations grouped by API domain
- a facade abstraction that aggregates the generated services
- dependency injection bootstrap structure
- separation between generated code and author-owned code

## Detected API Groups

{{groupsSection}}

## Structure

- `src/{{projectName}}.ApiClient`
  - Client generated from OpenAPI using Kiota

- `src/{{projectName}}.Core`
  - Contracts and shared models for API consumo
  - Includes:
    - `Interfaces/IApiFacade.cs`
{{IndentLines(interfaceFilesSection, 4)}}
    - `Models/ApiOptions.cs`

- `src/{{projectName}}.Infrastructure`
  - Concrete implementation layer for generated services and facades
  - Includes:
{{IndentLines(serviceFilesSection, 4)}}
    - `Facades/ApiFacade.cs`
    - `DependencyInjection/ServiceCollectionExtensions.cs`

## Base Namespace

`{{baseNamespace}}`

## Notes

This generated structure is intended as a modernization starting point. It can evolve to include:

- real `.csproj` files
- real `.sln` project entries
- method generation by endpoint
- integration with the exact Kiota client root type
- authentication pipeline support
- strongly typed domain-oriented service operations
""";

        File.WriteAllText(filePath, content);
    }

    private static string ToCamelCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        if (value.Length == 1)
            return value.ToLowerInvariant();

        return char.ToLowerInvariant(value[0]) + value[1..];
    }

    private static string IndentLines(string value, int spaces)
    {
        var indent = new string(' ', spaces);

        var lines = value.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        return string.Join(Environment.NewLine, lines.Select(line => $"{indent}{line}"));
    }
}