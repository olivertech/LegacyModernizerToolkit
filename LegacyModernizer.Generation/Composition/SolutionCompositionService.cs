namespace LegacyModernizer.Generation.Composition;

public sealed class SolutionCompositionService : ISolutionCompositionService
{
    /*
     * Saída esperada da composição:
     * 
     * {ProjectName}/
     *     {ProjectName}.sln
     *     README.md
     *     src/
     *     {ProjectName}.ApiClient/
     *         ... arquivos Kiota ...
     *     {ProjectName}.Core/
     *         Interfaces/
     *           IApiFacade.cs                      -> Contrato da facade
     *           IGeneratedApiService.cs            -> Contrato do serviço base
     *         Models/
     *             ApiOptions.cs                    -> Configuração base
     *     {ProjectName}.Infrastructure/
     *         Facades/
     *             ApiFacade.cs                     -> Orquestra o IGeneratedApiService
     *         Services/
     *             GeneratedApiService.cs           -> Implementação que conhece o cliente Kiota gerado
     *         DependencyInjection/
     *             ServiceCollectionExtensions.cs   -> Registra todas as injeções de dependência
     * 
     * O serviço de composição é responsável por criar a estrutura de pastas e arquivos necessários para a solução modernizada.
     * Ele deve validar os inputs, garantir que o workspace esteja preparado, e organizar os artefatos gerados de forma coerente.
     * A solução resultante deve ser fácil de entender e navegar, mesmo para desenvolvedores que não estão familiarizados com o processo de modernização.
     */
    public Task<ModernizedSolution> ComposeAsync(ModernizationRequest request,
                                                 Workspace workspace,
                                                 GeneratedArtifact generatedClientArtifact,
                                                 CancellationToken cancellationToken = default)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        if (workspace is null)
            throw new ArgumentNullException(nameof(workspace));

        if (generatedClientArtifact is null)
            throw new ArgumentNullException(nameof(generatedClientArtifact));

        if (!workspace.IsPrepared)
            throw new InvalidOperationException("Workspace must be prepared before solution composition.");

        if (generatedClientArtifact.Type != ArtifactType.GeneratedClient)
            throw new InvalidOperationException("The provided artifact is not a generated client artifact.");

        var generatedClientPath = generatedClientArtifact.Location.FullPath;

        if (!Directory.Exists(generatedClientPath))
            throw new DirectoryNotFoundException($"Generated client directory was not found: {generatedClientPath}");

        var projectName = request.ProjectName.ToString();
        var baseNamespace = request.BaseNamespace.ToString();

        var solutionRootPath = Path.Combine(workspace.Paths.ComposedPath, projectName);
        var srcPath = Path.Combine(solutionRootPath, "src");

        var apiClientProjectPath = Path.Combine(srcPath, $"{projectName}.ApiClient");
        var coreProjectPath = Path.Combine(srcPath, $"{projectName}.Core");
        var infrastructureProjectPath = Path.Combine(srcPath, $"{projectName}.Infrastructure");

        var coreInterfacesPath = Path.Combine(coreProjectPath, "Interfaces");
        var coreModelsPath = Path.Combine(coreProjectPath, "Models");

        var infrastructureFacadesPath = Path.Combine(infrastructureProjectPath, "Facades");
        var infrastructureServicesPath = Path.Combine(infrastructureProjectPath, "Services");
        var infrastructureDependencyInjectionPath = Path.Combine(infrastructureProjectPath, "DependencyInjection");

        Directory.CreateDirectory(solutionRootPath);
        Directory.CreateDirectory(srcPath);

        Directory.CreateDirectory(coreProjectPath);
        Directory.CreateDirectory(coreInterfacesPath);
        Directory.CreateDirectory(coreModelsPath);

        Directory.CreateDirectory(infrastructureProjectPath);
        Directory.CreateDirectory(infrastructureFacadesPath);
        Directory.CreateDirectory(infrastructureServicesPath);
        Directory.CreateDirectory(infrastructureDependencyInjectionPath);

        CopyDirectory(generatedClientPath, apiClientProjectPath);

        var solutionFilePath = Path.Combine(solutionRootPath, $"{projectName}.sln");
        CreatePlaceholderSolutionFile(solutionFilePath, projectName);

        CreateApiOptionsFile(coreModelsPath, baseNamespace);
        CreateGeneratedApiServiceInterfaceFile(coreInterfacesPath, baseNamespace);
        CreateApiFacadeInterfaceFile(coreInterfacesPath, baseNamespace);
        CreateGeneratedApiServiceFile(infrastructureServicesPath, baseNamespace);
        CreateApiFacadeFile(infrastructureFacadesPath, baseNamespace);
        CreateServiceCollectionExtensionsFile(infrastructureDependencyInjectionPath, baseNamespace);
        CreateReadmeFile(solutionRootPath, request);

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
$"""
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
# Placeholder solution generated by Legacy Modernizer Toolkit
# Project: {projectName}
# Note: This solution structure is ready to evolve into a fully compilable multi-project solution.
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

    private static void CreateGeneratedApiServiceInterfaceFile(string coreInterfacesPath, string baseNamespace)
    {
        var filePath = Path.Combine(coreInterfacesPath, "IGeneratedApiService.cs");

        var content =
$$"""
namespace {{baseNamespace}}.Core.Interfaces;

public interface IGeneratedApiService
{
    string BaseUrl { get; }
}
""";

        File.WriteAllText(filePath, content);
    }

    private static void CreateApiFacadeInterfaceFile(string coreInterfacesPath, string baseNamespace)
    {
        var filePath = Path.Combine(coreInterfacesPath, "IApiFacade.cs");

        var content =
$$"""
namespace {{baseNamespace}}.Core.Interfaces;

public interface IApiFacade
{
    IGeneratedApiService GeneratedApi { get; }
}
""";

        File.WriteAllText(filePath, content);
    }

    private static void CreateGeneratedApiServiceFile(string infrastructureServicesPath, string baseNamespace)
    {
        var filePath = Path.Combine(infrastructureServicesPath, "GeneratedApiService.cs");

        var content =
$$"""
using {{baseNamespace}}.Core.Interfaces;
using {{baseNamespace}}.Core.Models;

namespace {{baseNamespace}}.Infrastructure.Services;

public sealed class GeneratedApiService : IGeneratedApiService
{
    private readonly ApiOptions _options;

    public GeneratedApiService(ApiOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public string BaseUrl => _options.BaseUrl;
}
""";

        File.WriteAllText(filePath, content);
    }

    private static void CreateApiFacadeFile(string infrastructureFacadesPath, string baseNamespace)
    {
        var filePath = Path.Combine(infrastructureFacadesPath, "ApiFacade.cs");

        var content =
$$"""
using {{baseNamespace}}.Core.Interfaces;

namespace {{baseNamespace}}.Infrastructure.Facades;

public sealed class ApiFacade : IApiFacade
{
    public ApiFacade(IGeneratedApiService generatedApi)
    {
        GeneratedApi = generatedApi ?? throw new ArgumentNullException(nameof(generatedApi));
    }

    public IGeneratedApiService GeneratedApi { get; }
}
""";

        File.WriteAllText(filePath, content);
    }

    private static void CreateServiceCollectionExtensionsFile(string infrastructureDependencyInjectionPath, string baseNamespace)
    {
        var filePath = Path.Combine(infrastructureDependencyInjectionPath, "ServiceCollectionExtensions.cs");

        var content =
$$"""
using {{baseNamespace}}.Core.Interfaces;
using {{baseNamespace}}.Core.Models;
using {{baseNamespace}}.Infrastructure.Facades;
using {{baseNamespace}}.Infrastructure.Services;
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
        services.AddScoped<IGeneratedApiService, GeneratedApiService>();
        services.AddScoped<IApiFacade, ApiFacade>();

        return services;
    }
}
""";

        File.WriteAllText(filePath, content);
    }

    private static void CreateReadmeFile(string solutionRootPath, ModernizationRequest request)
    {
        var projectName = request.ProjectName.ToString();
        var baseNamespace = request.BaseNamespace.ToString();

        var filePath = Path.Combine(solutionRootPath, "README.md");

        var content =
$$"""
# {{projectName}}

This solution was generated by Legacy Modernizer Toolkit.

## Purpose

This output represents a first modernization layer over an OpenAPI/Swagger specification, combining:

- a generated API client
- service abstractions
- facade abstractions
- dependency injection bootstrap structure
- a cleaner separation between generated code and author-owned code

## Structure

- `src/{{projectName}}.ApiClient`
  - Client generated from OpenAPI using Kiota

- `src/{{projectName}}.Core`
  - Contracts and shared models for API consumption
  - Includes:
    - `Interfaces/IApiFacade.cs`
    - `Interfaces/IGeneratedApiService.cs`
    - `Models/ApiOptions.cs`

- `src/{{projectName}}.Infrastructure`
  - Concrete implementation layer for services and facades
  - Includes:
    - `Services/GeneratedApiService.cs`
    - `Facades/ApiFacade.cs`
    - `DependencyInjection/ServiceCollectionExtensions.cs`

## Base Namespace

`{{baseNamespace}}`

## Notes

This generated structure is designed as a modernization starting point and can be evolved into a fully compilable multi-project solution with:

- real `.csproj` files
- real `.sln` project entries
- domain-oriented service generation by endpoint groups or tags
- API-specific facades and services
- authentication pipeline integration
""";

        File.WriteAllText(filePath, content);
    }
}
