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
                                                 KiotaClientMetadata kiotaMetadata,
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

        if (kiotaMetadata is null)
            throw new ArgumentNullException(nameof(kiotaMetadata));

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
            .GroupBy(x => x.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(x => x.Name)
            .ToArray();

        var solutionRootPath = Path.Combine(workspace.Paths.ComposedPath, projectName);
        var srcPath = Path.Combine(solutionRootPath, "src");

        var apiClientProjectPath = Path.Combine(srcPath, $"{projectName}.ApiClient");
        var coreProjectPath = Path.Combine(srcPath, $"{projectName}.Core");
        var infrastructureProjectPath = Path.Combine(srcPath, $"{projectName}.Infrastructure");

        var coreInterfacesPath = Path.Combine(coreProjectPath, "Interfaces");

        var infrastructureFacadesPath = Path.Combine(infrastructureProjectPath, "Facades");
        var infrastructureServicesPath = Path.Combine(infrastructureProjectPath, "Services");
        var infrastructureDependencyInjectionPath = Path.Combine(infrastructureProjectPath, "DependencyInjection");

        Directory.CreateDirectory(solutionRootPath);
        Directory.CreateDirectory(srcPath);

        Directory.CreateDirectory(apiClientProjectPath);
        Directory.CreateDirectory(coreProjectPath);
        Directory.CreateDirectory(coreInterfacesPath);

        Directory.CreateDirectory(infrastructureProjectPath);
        Directory.CreateDirectory(infrastructureFacadesPath);
        Directory.CreateDirectory(infrastructureServicesPath);
        Directory.CreateDirectory(infrastructureDependencyInjectionPath);

        CopyDirectory(generatedClientPath, apiClientProjectPath);

        CreateProjectFiles(projectName,request.TargetFramework, apiClientProjectPath, coreProjectPath, infrastructureProjectPath);
        CreateSolutionFileWithDotNetCli(solutionRootPath, projectName, coreProjectPath, apiClientProjectPath, infrastructureProjectPath);

        var solutionFilePath = Path.Combine(solutionRootPath, $"{projectName}.sln");

        //CreatePlaceholderSolutionFile(solutionFilePath, projectName);
        CreateApiFacadeInterfaceFile(coreInterfacesPath, baseNamespace, normalizedGroups);
        CreateApiFacadeBaseFile(infrastructureFacadesPath, baseNamespace, kiotaMetadata);

        foreach (var group in normalizedGroups)
        {
            CreateApiFacadePartialFile(infrastructureFacadesPath, baseNamespace, group, kiotaMetadata);
            CreateServiceInterfaceFile(coreInterfacesPath, baseNamespace, group);
            CreateServiceImplementationFile(infrastructureServicesPath, baseNamespace, group);
        }

        CreateServiceCollectionExtensionsFile(
            infrastructureDependencyInjectionPath,
            baseNamespace,
            normalizedGroups,
            kiotaMetadata);

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
                Directory.CreateDirectory(destinationDirectory);

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

    private static void CreateApiFacadeInterfaceFile(
        string coreInterfacesPath,
        string baseNamespace,
        IReadOnlyCollection<ApiGroupDefinition> groups)
    {
        var filePath = Path.Combine(coreInterfacesPath, "IApiFacade.cs");

        var methodDefinitions = BuildFacadeInterfaceMethods(groups);

        var content =
$$"""
using System.Threading;
using System.Threading.Tasks;

namespace {{baseNamespace}}.Core.Interfaces;

public interface IApiFacade
{
{{methodDefinitions}}
}
""";

        File.WriteAllText(filePath, content);
    }

    private static void CreateApiFacadeBaseFile(string infrastructureFacadesPath,
                                                string baseNamespace,
                                                KiotaClientMetadata kiotaMetadata)
    {
        var filePath = Path.Combine(infrastructureFacadesPath, "ApiFacade.cs");

        var clientClassName = string.IsNullOrWhiteSpace(kiotaMetadata.ClientClassName)
            ? "GeneratedApiClient"
            : kiotaMetadata.ClientClassName;

        var rootNamespace = string.IsNullOrWhiteSpace(kiotaMetadata.RootNamespace)
            ? baseNamespace
            : kiotaMetadata.RootNamespace;

        var builderProperties = kiotaMetadata.Groups.Count == 0
            ? "    // No Kiota request builders were detected."
            : string.Join(
                Environment.NewLine,
                kiotaMetadata.Groups.Select(group =>
                    $"    private dynamic? {group.GroupName}Api => _apiClient.{group.BuilderAccessExpression};"));

        var content =
$$"""
using System;
using System.Net.Http;
using {{rootNamespace}};
using {{baseNamespace}}.Core.Interfaces;

namespace {{baseNamespace}}.Infrastructure.Facades;

public sealed partial class ApiFacade : IApiFacade
{
    private readonly {{clientClassName}} _apiClient;
    private readonly IHttpClientFactory _httpClientFactory;

{{builderProperties}}

    public ApiFacade(
        {{clientClassName}} apiClient,
        IHttpClientFactory httpClientFactory)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    }
}
""";

        File.WriteAllText(filePath, content);
    }

    private static void CreateApiFacadePartialFile(string infrastructureFacadesPath,
                                                   string baseNamespace,
                                                   ApiGroupDefinition group,
                                                   KiotaClientMetadata kiotaMetadata)
    {
        var groupName = group.Name.Trim();
        var filePath = Path.Combine(infrastructureFacadesPath, $"ApiFacade.{groupName}.cs");

        var methods = BuildFacadePartialMethods(group, kiotaMetadata);

        var content =
$$"""
using System.Threading;
using System.Threading.Tasks;

namespace {{baseNamespace}}.Infrastructure.Facades;

public sealed partial class ApiFacade
{
{{methods}}
}
""";

        File.WriteAllText(filePath, content);
    }

    private static void CreateServiceInterfaceFile(string coreInterfacesPath,
                                                   string baseNamespace,
                                                   ApiGroupDefinition group)
    {
        var groupName = group.Name.Trim();
        var filePath = Path.Combine(coreInterfacesPath, $"I{groupName}Service.cs");

        var methods = BuildServiceInterfaceMethods(group);

        var content =
$$"""
using System.Threading;
using System.Threading.Tasks;

namespace {{baseNamespace}}.Core.Interfaces;

public interface I{{groupName}}Service
{
{{methods}}
}
""";

        File.WriteAllText(filePath, content);
    }

    private static void CreateServiceImplementationFile(string infrastructureServicesPath,
                                                        string baseNamespace,
                                                        ApiGroupDefinition group)
    {
        var groupName = group.Name.Trim();
        var filePath = Path.Combine(infrastructureServicesPath, $"{groupName}Service.cs");

        var methods = BuildServiceImplementationMethods(group);

        var content =
$$"""
using System;
using System.Threading;
using System.Threading.Tasks;
using {{baseNamespace}}.Core.Interfaces;

namespace {{baseNamespace}}.Infrastructure.Services;

public sealed class {{groupName}}Service : I{{groupName}}Service
{
    private readonly IApiFacade _apiFacade;

    public {{groupName}}Service(IApiFacade apiFacade)
    {
        _apiFacade = apiFacade ?? throw new ArgumentNullException(nameof(apiFacade));
    }

{{methods}}
}
""";

        File.WriteAllText(filePath, content);
    }

    private static void CreateServiceCollectionExtensionsFile(string infrastructureDependencyInjectionPath,
                                                              string baseNamespace,
                                                              IReadOnlyCollection<ApiGroupDefinition> groups,
                                                              KiotaClientMetadata kiotaMetadata)
    {
        var filePath = Path.Combine(infrastructureDependencyInjectionPath, "ServiceCollectionExtensions.cs");

        var serviceRegistrations = groups.Count == 0
            ? "        // No API groups were detected."
            : string.Join(Environment.NewLine, groups.Select(g => $"        services.AddScoped<I{g.Name}Service, {g.Name}Service>();"));

        var content =
$$"""
using System;
using {{baseNamespace}}.Core.Interfaces;
using {{baseNamespace}}.Infrastructure.Facades;
using {{baseNamespace}}.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace {{baseNamespace}}.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGeneratedApi(this IServiceCollection services)
    {
        if (services is null)
            throw new ArgumentNullException(nameof(services));

        services.AddScoped<IApiFacade, ApiFacade>();

{{serviceRegistrations}}

        return services;
    }
}
""";

        File.WriteAllText(filePath, content);
    }

    private static void CreateReadmeFile(string solutionRootPath,
                                         ModernizationRequest request,
                                         IReadOnlyCollection<ApiGroupDefinition> groups)
    {
        var projectName = request.ProjectName.ToString();
        var baseNamespace = request.BaseNamespace.ToString();

        var groupsSection = groups.Count == 0
            ? "- No API groups were detected during composition."
            : string.Join(Environment.NewLine, groups.Select(g => $"- `{g.Name}` ({g.Endpoints.Count} endpoints)"));

        var filePath = Path.Combine(solutionRootPath, "README.md");

        var content =
$$"""
# {{projectName}}

This solution was generated by Legacy Modernizer Toolkit.

## Purpose

This output represents a modernization layer over an OpenAPI/Swagger specification using:

- Kiota generated client
- partial API facade files grouped by API area
- application services consuming the generated facade
- dependency injection bootstrap structure

## Detected API Groups

{{groupsSection}}

## Base Namespace

`{{baseNamespace}}`

## Notes

The generated facade follows the partial class pattern, allowing each API area to be maintained in a separate file.
""";

        File.WriteAllText(filePath, content);
    }

    private static string BuildFacadeInterfaceMethods(IReadOnlyCollection<ApiGroupDefinition> groups)
    {
        var methods = groups
            .SelectMany(g => g.Endpoints)
            .Select(e => new
            {
                MethodName = NormalizeMethodName(e.OperationId, e.Path, e.Method),
                Endpoint = e
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.MethodName))
            .GroupBy(x => x.MethodName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(x => x.MethodName)
            .Select(x => $"    Task<object?> {x.MethodName}Async({BuildMethodParameters(x.Endpoint)});");

        return string.Join(Environment.NewLine + Environment.NewLine, methods);
    }

    private static string BuildFacadePartialMethods(ApiGroupDefinition group, KiotaClientMetadata kiotaMetadata)
    {
        var methods = group.Endpoints
            .Select(e => new
            {
                MethodName = NormalizeMethodName(e.OperationId, e.Path, e.Method),
                Endpoint = e
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.MethodName))
            .GroupBy(x => x.MethodName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(x => x.MethodName)
            .Select(x =>
            {
                var parameters = BuildMethodParameters(x.Endpoint);
                var kiotaCallExpression = BuildKiotaCallExpression(
                    group.Name,
                    x.Endpoint,
                    kiotaMetadata);

                return
    $$"""
    public async Task<object?> {{x.MethodName}}Async({{parameters}})
    {
        var result = await {{kiotaCallExpression}}.ConfigureAwait(false);

        return result;
    }
""";
            });

        return string.Join(Environment.NewLine + Environment.NewLine, methods);
    }

    private static string BuildServiceInterfaceMethods(ApiGroupDefinition group)
    {
        var methods = group.Endpoints
            .Select(e => new
            {
                MethodName = NormalizeMethodName(e.OperationId, e.Path, e.Method),
                Endpoint = e
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.MethodName))
            .GroupBy(x => x.MethodName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(x => x.MethodName)
            .Select(x => $"    Task<object?> {x.MethodName}Async({BuildMethodParameters(x.Endpoint)});");

        return string.Join(Environment.NewLine + Environment.NewLine, methods);
    }

    private static string BuildServiceImplementationMethods(ApiGroupDefinition group)
    {
        var methods = group.Endpoints
            .Select(e => new
            {
                MethodName = NormalizeMethodName(e.OperationId, e.Path, e.Method),
                Endpoint = e
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.MethodName))
            .GroupBy(x => x.MethodName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(x => x.MethodName)
            .Select(x =>
            {
                var parameters = BuildMethodParameters(x.Endpoint);
                var arguments = BuildMethodArguments(x.Endpoint);

                return
    $$"""
    public Task<object?> {{x.MethodName}}Async({{parameters}})
    {
        return _apiFacade.{{x.MethodName}}Async({{arguments}});
    }
""";
            });

        return string.Join(Environment.NewLine + Environment.NewLine, methods);
    }

    private static string BuildKiotaCallExpression(
        string groupName,
        ApiEndpointDefinition endpoint,
        KiotaClientMetadata kiotaMetadata)
    {
        var groupMetadata = kiotaMetadata.Groups
            .FirstOrDefault(x => x.GroupName.Equals(groupName, StringComparison.OrdinalIgnoreCase));

        var groupApiProperty = groupMetadata is null
            ? $"{NormalizeIdentifier(groupName)}Api"
            : $"{groupMetadata.GroupName}Api";

        var remainingSegments = ExtractSegmentsAfterGroup(endpoint.Path, groupName)
            .Select(NormalizeIdentifier)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();

        var builderChain = remainingSegments.Length == 0
            ? groupApiProperty
            : $"{groupApiProperty}!.{string.Join(".", remainingSegments)}";

        var asyncMethodName = GetKiotaAsyncMethodName(endpoint.Method);
        var configBlock = BuildKiotaRequestConfiguration(endpoint);
        var bodyArgument = endpoint.HasRequestBody ? "body, " : string.Empty;

        if (string.IsNullOrWhiteSpace(configBlock))
            return $"{builderChain}.{asyncMethodName}({bodyArgument}cancellationToken: cancellationToken)";

        return $"{builderChain}.{asyncMethodName}({bodyArgument}config =>{Environment.NewLine}        {{{Environment.NewLine}{configBlock}{Environment.NewLine}        }}, cancellationToken: cancellationToken)";
    }

    private static string[] ExtractSegmentsAfterGroup(string path, string groupName)
    {
        var segments = path
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !IsVersionSegment(x))
            .Where(x => !x.StartsWith("{") && !x.EndsWith("}"))
            .ToArray();

        var groupIndex = Array.FindIndex(
            segments,
            x => NormalizeIdentifier(x).Equals(groupName, StringComparison.OrdinalIgnoreCase));

        if (groupIndex < 0 || groupIndex + 1 >= segments.Length)
            return Array.Empty<string>();

        return segments
            .Skip(groupIndex + 1)
            .ToArray();
    }

    private static string GetKiotaAsyncMethodName(string httpMethod)
    {
        return httpMethod.ToUpperInvariant() switch
        {
            "GET" => "GetAsync",
            "POST" => "PostAsync",
            "PUT" => "PutAsync",
            "PATCH" => "PatchAsync",
            "DELETE" => "DeleteAsync",
            _ => "GetAsync"
        };
    }

    private static string NormalizeMethodName(string? operationId, string path, string method)
    {
        if (!string.IsNullOrWhiteSpace(operationId))
            return NormalizeIdentifier(operationId);

        var pathSegments = path
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(segment => !segment.StartsWith("{") && !segment.EndsWith("}"))
            .Where(segment => !IsVersionSegment(segment))
            .Select(NormalizeIdentifier)
            .Where(segment => !string.IsNullOrWhiteSpace(segment));

        var composedName = string.Concat(pathSegments);

        if (string.IsNullOrWhiteSpace(composedName))
            composedName = "Operation";

        return NormalizeIdentifier(method) + composedName;
    }

    private static string NormalizeIdentifier(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
            return string.Empty;

        var parts = rawValue
            .Split(new[] { '-', '_', '.', '/', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => new string(part.Where(char.IsLetterOrDigit).ToArray()))
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToArray();

        if (parts.Length == 0)
            return string.Empty;

        return string.Concat(parts.Select(p => p.Length == 1
            ? p.ToUpperInvariant()
            : char.ToUpperInvariant(p[0]) + p[1..]));
    }

    private static bool IsVersionSegment(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
            return false;

        segment = segment.Trim();

        if (segment.Length < 2)
            return false;

        if (segment[0] != 'v' && segment[0] != 'V')
            return false;

        return char.IsDigit(segment[1]);
    }

    private static string BuildMethodParameters(ApiEndpointDefinition endpoint)
    {
        var parameters = new List<string>();

        if (endpoint.RequiresAuthorization)
            parameters.Add("string? accessToken = null");

        foreach (var parameter in endpoint.Parameters
            .Where(x => x.Location.Equals("path", StringComparison.OrdinalIgnoreCase)))
        {
            parameters.Add($"string {ToCamelCase(NormalizeIdentifier(parameter.Name))}");
        }

        foreach (var parameter in endpoint.Parameters
            .Where(x => x.Location.Equals("query", StringComparison.OrdinalIgnoreCase)))
        {
            parameters.Add($"string? {ToCamelCase(NormalizeIdentifier(parameter.Name))} = null");
        }

        foreach (var parameter in endpoint.Parameters
            .Where(x => x.Location.Equals("header", StringComparison.OrdinalIgnoreCase)))
        {
            parameters.Add($"string? {ToCamelCase(NormalizeIdentifier(parameter.Name))} = null");
        }

        if (endpoint.HasRequestBody)
            parameters.Add("object? body = null");

        parameters.Add("CancellationToken cancellationToken = default");

        return string.Join(", ", parameters);
    }

    private static string BuildMethodArguments(ApiEndpointDefinition endpoint)
    {
        var arguments = new List<string>();

        if (endpoint.RequiresAuthorization)
            arguments.Add("accessToken");

        foreach (var parameter in endpoint.Parameters
            .Where(x =>
                x.Location.Equals("path", StringComparison.OrdinalIgnoreCase) ||
                x.Location.Equals("query", StringComparison.OrdinalIgnoreCase) ||
                x.Location.Equals("header", StringComparison.OrdinalIgnoreCase)))
        {
            arguments.Add(ToCamelCase(NormalizeIdentifier(parameter.Name)));
        }

        if (endpoint.HasRequestBody)
            arguments.Add("body");

        arguments.Add("cancellationToken");

        return string.Join(", ", arguments);
    }

    private static string BuildKiotaRequestConfiguration(ApiEndpointDefinition endpoint)
    {
        var lines = new List<string>();

        if (endpoint.RequiresAuthorization)
        {
            lines.Add("            if (!string.IsNullOrWhiteSpace(accessToken))");
            lines.Add("                config.Headers.Add(\"Authorization\", $\"Bearer {accessToken}\");");
        }

        foreach (var header in endpoint.Parameters
            .Where(x => x.Location.Equals("header", StringComparison.OrdinalIgnoreCase)))
        {
            var parameterName = ToCamelCase(NormalizeIdentifier(header.Name));
            lines.Add($"            if (!string.IsNullOrWhiteSpace({parameterName}))");
            lines.Add($"                config.Headers.Add(\"{header.Name}\", {parameterName});");
        }

        foreach (var query in endpoint.Parameters
            .Where(x => x.Location.Equals("query", StringComparison.OrdinalIgnoreCase)))
        {
            var parameterName = ToCamelCase(NormalizeIdentifier(query.Name));
            var propertyName = NormalizeIdentifier(query.Name);

            lines.Add($"            config.QueryParameters.{propertyName} = {parameterName};");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string ToCamelCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        if (value.Length == 1)
            return value.ToLowerInvariant();

        return char.ToLowerInvariant(value[0]) + value[1..];
    }

    private static void CreateProjectFiles(string projectName,
                                           string targetFramework,
                                           string apiClientProjectPath,
                                           string coreProjectPath,
                                           string infrastructureProjectPath)
    {
        CreateApiClientProjectFile(projectName, targetFramework, apiClientProjectPath);
        CreateCoreProjectFile(projectName, targetFramework, coreProjectPath);
        CreateInfrastructureProjectFile(projectName, targetFramework, infrastructureProjectPath);
    }

    private static void CreateApiClientProjectFile(string projectName, string targetFramework, string apiClientProjectPath)
    {
        var filePath = Path.Combine(apiClientProjectPath, $"{projectName}.ApiClient.csproj");

        var content =
    $$"""
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>{{targetFramework}}</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Kiota.Abstractions" Version="1.*" />
    <PackageReference Include="Microsoft.Kiota.Http.HttpClientLibrary" Version="1.*" />
    <PackageReference Include="Microsoft.Kiota.Serialization.Json" Version="1.*" />
    <PackageReference Include="Microsoft.Kiota.Serialization.Text" Version="1.*" />
    <PackageReference Include="Microsoft.Kiota.Serialization.Form" Version="1.*" />
    <PackageReference Include="Microsoft.Kiota.Serialization.Multipart" Version="1.*" />
  </ItemGroup>

</Project>
""";

        File.WriteAllText(filePath, content);
    }

    private static void CreateCoreProjectFile(string projectName, string targetFramework, string coreProjectPath)
    {
        var filePath = Path.Combine(coreProjectPath, $"{projectName}.Core.csproj");

        var content =
    $$"""
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>{{targetFramework}}</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>

</Project>
""";

        File.WriteAllText(filePath, content);
    }

    private static void CreateInfrastructureProjectFile(string projectName, string targetFramework, string infrastructureProjectPath)
    {
        var filePath = Path.Combine(infrastructureProjectPath, $"{projectName}.Infrastructure.csproj");

        var content =
    $$"""
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>{{targetFramework}}</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\{{projectName}}.Core\{{projectName}}.Core.csproj" />
    <ProjectReference Include="..\{{projectName}}.ApiClient\{{projectName}}.ApiClient.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="8.*" />
    <PackageReference Include="Microsoft.Extensions.Http" Version="8.*" />
  </ItemGroup>

</Project>
""";

        File.WriteAllText(filePath, content);
    }

    private static void CreateSolutionFileWithDotNetCli(
    string solutionRootPath,
    string projectName,
    string coreProjectPath,
    string apiClientProjectPath,
    string infrastructureProjectPath)
    {
        Directory.CreateDirectory(solutionRootPath);

        var solutionFilePath = Path.Combine(solutionRootPath, $"{projectName}.sln");

        if (File.Exists(solutionFilePath))
            File.Delete(solutionFilePath);

        RunDotNetCommand(
            solutionRootPath,
            ["new", "sln", "--name", projectName, "--output", solutionRootPath, "--format", "sln"]);

        if (!File.Exists(solutionFilePath))
        {
            var existingSlnFiles = Directory.GetFiles(solutionRootPath, "*.sln", SearchOption.AllDirectories);

            var foundFiles = existingSlnFiles.Length == 0
                ? "No .sln files found."
                : string.Join(Environment.NewLine, existingSlnFiles);

            throw new FileNotFoundException(
                $"The solution file was not created by dotnet CLI. Expected: {solutionFilePath}. Found: {foundFiles}",
                solutionFilePath);
        }

        RunDotNetCommand(
            solutionRootPath,
            ["sln", solutionFilePath, "add", Path.Combine(coreProjectPath, $"{projectName}.Core.csproj")]);

        RunDotNetCommand(
            solutionRootPath,
            ["sln", solutionFilePath, "add", Path.Combine(apiClientProjectPath, $"{projectName}.ApiClient.csproj")]);

        RunDotNetCommand(
            solutionRootPath,
            ["sln", solutionFilePath, "add", Path.Combine(infrastructureProjectPath, $"{projectName}.Infrastructure.csproj")]);
    }

    private static void RunDotNetCommand(string workingDirectory, IReadOnlyList<string> arguments)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = new System.Diagnostics.Process
        {
            StartInfo = startInfo
        };

        process.Start();

        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();

        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"dotnet command failed. Command: dotnet {string.Join(" ", arguments)}. Error: {standardError}. Output: {standardOutput}");
        }
    }
}