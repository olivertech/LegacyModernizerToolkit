namespace LegacyModernizer.Generation.Composition;

/// <summary>
/// Converte a saída do Kiota e os grupos de API em uma solução .NET pronta para consumo.
/// </summary>
public sealed class SolutionCompositionService : ISolutionCompositionService
{
    private const string KiotaAbstractionsPackageVersion = "1.22.1";
    private const string KiotaHttpPackageVersion = "1.22.1";
    private const string KiotaSerializationJsonPackageVersion = "1.22.1";
    private const string KiotaSerializationTextPackageVersion = "1.22.1";
    private const string KiotaSerializationFormPackageVersion = "1.22.1";
    private const string KiotaSerializationMultipartPackageVersion = "1.22.1";
    private const string MicrosoftExtensionsDependencyInjectionAbstractionsPackageVersion = "10.0.6";
    private const string MicrosoftExtensionsHttpPackageVersion = "10.0.6";

    private static readonly System.Text.RegularExpressions.Regex DtoPropertyRegex =
        new(
            @"public\s+(?<type>(?:global::)?[A-Za-z0-9_.<>,\s\?\[\]]+)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{\s*get;\s*set;\s*\}",
            System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.Singleline);

    private static readonly System.Text.RegularExpressions.Regex EnumRegex =
        new(
            @"public\s+enum\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{(?<body>[\s\S]*?)\}",
            System.Text.RegularExpressions.RegexOptions.Compiled);

    private static readonly System.Text.RegularExpressions.Regex EnumMemberLineRegex =
        new(
            @"^\s*(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*(?:=\s*[^,]+)?\s*,?\s*$",
            System.Text.RegularExpressions.RegexOptions.Compiled);

    private static readonly string[] CollectionTypeNames =
    [
        "List",
        "IList",
        "ICollection",
        "IEnumerable"
    ];

    /// <summary>
    /// Contexto interno usado para acompanhar quais tipos do Kiota já foram convertidos em DTOs gerados.
    /// </summary>
    private sealed class DtoGenerationContext
    {
        public required string BaseNamespace { get; init; }
        public required string ClientRootPath { get; init; }
        public Dictionary<string, string> SourceToDtoTypeName { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> DtoFiles { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> ValueTypeDtos { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Compõe a solution final copiando o client Kiota, gerando contratos, camada HTTP e artefatos auxiliares.
    /// </summary>
    public Task<ModernizedSolution> ComposeAsync(
        ModernizationRequest request,
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
        var targetFramework = request.TargetFramework ?? "net8.0";
        var projectLayout = ProjectNamingStrategyResolver.Resolve(request);

        var normalizedGroups = groups
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .GroupBy(x => x.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            // Algumas specs repetem grupos com pequenas diferenças de casing ou ordem.
            // Aqui consolidamos a visão final antes de gerar contratos e services.
            .Select(g => g.First())
            .OrderBy(x => x.Name)
            .ToArray();

        var dtoContext = BuildDtoGenerationContext(
            generatedClientPath,
            projectLayout.ContractsNamespace,
            normalizedGroups,
            kiotaMetadata);

        var solutionRootPath = Path.Combine(workspace.Paths.ComposedPath, projectLayout.SolutionRootFolderName);
        var srcPath = Path.Combine(solutionRootPath, "src");

        var apiClientProjectPath = Path.Combine(srcPath, projectLayout.ApiClientProjectName);
        var coreProjectPath = Path.Combine(srcPath, projectLayout.ContractsProjectName);
        var infrastructureProjectPath = Path.Combine(srcPath, projectLayout.HttpProjectName);

        var coreInterfacesPath = Path.Combine(coreProjectPath, "Interfaces");
        var coreDtosPath = Path.Combine(coreProjectPath, "Dtos");

        var infrastructureFacadesPath = Path.Combine(infrastructureProjectPath, "Facades");
        var infrastructureAuthenticationPath = Path.Combine(infrastructureProjectPath, "Authentication");
        var infrastructureMappersPath = Path.Combine(infrastructureProjectPath, "Mappers");
        var infrastructureServicesPath = Path.Combine(infrastructureProjectPath, "Services");
        var infrastructureDependencyInjectionPath = Path.Combine(infrastructureProjectPath, "DependencyInjection");

        Directory.CreateDirectory(solutionRootPath);
        Directory.CreateDirectory(srcPath);

        Directory.CreateDirectory(apiClientProjectPath);
        Directory.CreateDirectory(coreProjectPath);
        Directory.CreateDirectory(coreInterfacesPath);
        Directory.CreateDirectory(coreDtosPath);

        Directory.CreateDirectory(infrastructureProjectPath);
        Directory.CreateDirectory(infrastructureFacadesPath);
        Directory.CreateDirectory(infrastructureAuthenticationPath);
        Directory.CreateDirectory(infrastructureMappersPath);
        Directory.CreateDirectory(infrastructureServicesPath);
        Directory.CreateDirectory(infrastructureDependencyInjectionPath);

        CopyDirectory(generatedClientPath, apiClientProjectPath);

        RewriteKiotaClientNamespaces(
            apiClientProjectPath,
            request,
            kiotaMetadata,
            projectLayout.ApiClientNamespace);

        RenameKiotaClientClass(
            apiClientProjectPath,
            kiotaMetadata.ClientClassName,
            projectLayout.ClientClassName,
            projectLayout.ApiClientNamespace);

        var effectiveClientClassName = ResolveEffectiveClientClassName(
            apiClientProjectPath,
            projectLayout.ApiClientNamespace,
            projectLayout.ClientClassName,
            kiotaMetadata.ClientClassName);

        CreateProjectFiles(
            projectLayout,
            request.GenerationMode,
            targetFramework,
            apiClientProjectPath,
            coreProjectPath,
            infrastructureProjectPath);

        CreateDtoFiles(
            coreDtosPath,
            dtoContext);

        CreateSolutionFileWithDotNetCli(
            solutionRootPath,
            projectLayout,
            coreProjectPath,
            apiClientProjectPath,
            infrastructureProjectPath);

        CreateApiFacadeInterfaceFile(
            coreInterfacesPath,
            projectLayout,
            normalizedGroups,
            kiotaMetadata,
            dtoContext,
            request.AuthenticationMode);

        if (request.AuthenticationMode == AuthenticationMode.AccessTokenAccessor)
            CreateAccessTokenAccessorInterfaceFile(coreInterfacesPath, projectLayout);

        CreateApiFacadeBaseFile(
            infrastructureFacadesPath,
            projectLayout,
            effectiveClientClassName);

        if (request.AuthenticationMode == AuthenticationMode.AccessTokenAccessor)
        {
            CreateAccessTokenAuthenticationProviderFile(
                infrastructureAuthenticationPath,
                projectLayout);
        }

        foreach (var group in normalizedGroups)
        {
            CreateApiFacadePartialFile(
                infrastructureFacadesPath,
                projectLayout,
                group,
                kiotaMetadata,
                dtoContext,
                request.AuthenticationMode);

            CreateServiceInterfaceFile(
                coreInterfacesPath,
                projectLayout,
                group,
                kiotaMetadata,
                dtoContext,
                request.AuthenticationMode);

            CreateServiceImplementationFile(
                infrastructureServicesPath,
                projectLayout,
                group,
                kiotaMetadata,
                dtoContext,
                request.AuthenticationMode);
        }

        CreateDtoMapperFile(
            infrastructureMappersPath,
            projectLayout);

        CreateServiceCollectionExtensionsFile(
            infrastructureDependencyInjectionPath,
            projectLayout,
            normalizedGroups,
            effectiveClientClassName,
            request.AuthenticationMode);

        CreateGenerationManifestFile(
            solutionRootPath,
            request,
            projectLayout,
            normalizedGroups,
            kiotaMetadata,
            dtoContext);

        CreateReadmeFile(
            solutionRootPath,
            request,
            projectLayout,
            normalizedGroups);

        if (request.GenerationMode == GenerationMode.Embedded)
        {
            CreateIntegrationManifestFile(
                solutionRootPath,
                request,
                projectLayout,
                normalizedGroups);

            CreateIntegrationGuideFile(
                solutionRootPath,
                request,
                projectLayout,
                normalizedGroups);
        }

        var solution = new ModernizedSolution(
            request.ProjectName,
            request.BaseNamespace,
            solutionRootPath,
            Path.Combine(solutionRootPath, $"{projectLayout.SolutionName}.sln"));

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

    private static void CreateProjectFiles(
        SolutionProjectLayout projectLayout,
        GenerationMode generationMode,
        string targetFramework,
        string apiClientProjectPath,
        string coreProjectPath,
        string infrastructureProjectPath)
    {
        CreateApiClientProjectFile(projectLayout, generationMode, targetFramework, apiClientProjectPath);
        CreateCoreProjectFile(projectLayout, generationMode, targetFramework, coreProjectPath);
        CreateInfrastructureProjectFile(projectLayout, generationMode, targetFramework, infrastructureProjectPath);
    }

    private static void CreateApiClientProjectFile(
        SolutionProjectLayout projectLayout,
        GenerationMode generationMode,
        string targetFramework,
        string apiClientProjectPath)
    {
        var filePath = Path.Combine(apiClientProjectPath, $"{projectLayout.ApiClientProjectName}.csproj");
        var centralPackageManagementOptOut = generationMode == GenerationMode.Embedded
            ? "    <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>"
            : string.Empty;

        var content =
$$"""
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>{{targetFramework}}</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
{{centralPackageManagementOptOut}}
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Kiota.Abstractions" Version="{{KiotaAbstractionsPackageVersion}}" />
    <PackageReference Include="Microsoft.Kiota.Http.HttpClientLibrary" Version="{{KiotaHttpPackageVersion}}" />
    <PackageReference Include="Microsoft.Kiota.Serialization.Json" Version="{{KiotaSerializationJsonPackageVersion}}" />
    <PackageReference Include="Microsoft.Kiota.Serialization.Text" Version="{{KiotaSerializationTextPackageVersion}}" />
    <PackageReference Include="Microsoft.Kiota.Serialization.Form" Version="{{KiotaSerializationFormPackageVersion}}" />
    <PackageReference Include="Microsoft.Kiota.Serialization.Multipart" Version="{{KiotaSerializationMultipartPackageVersion}}" />
  </ItemGroup>

</Project>
""";

        File.WriteAllText(filePath, content);
    }

    private static void CreateCoreProjectFile(
        SolutionProjectLayout projectLayout,
        GenerationMode generationMode,
        string targetFramework,
        string coreProjectPath)
    {
        var filePath = Path.Combine(coreProjectPath, $"{projectLayout.ContractsProjectName}.csproj");
        var centralPackageManagementOptOut = generationMode == GenerationMode.Embedded
            ? "    <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>"
            : string.Empty;

        var content =
    $$"""
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>{{targetFramework}}</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
{{centralPackageManagementOptOut}}
  </PropertyGroup>

</Project>
""";

        File.WriteAllText(filePath, content);
    }

    private static void CreateInfrastructureProjectFile(
            SolutionProjectLayout projectLayout,
            GenerationMode generationMode,
            string targetFramework,
            string infrastructureProjectPath)
    {
        var filePath = Path.Combine(infrastructureProjectPath, $"{projectLayout.HttpProjectName}.csproj");
        var centralPackageManagementOptOut = generationMode == GenerationMode.Embedded
            ? "    <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>"
            : string.Empty;

        var content =
$$"""
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>{{targetFramework}}</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
{{centralPackageManagementOptOut}}
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\{{projectLayout.ContractsProjectName}}\{{projectLayout.ContractsProjectName}}.csproj" />
    <ProjectReference Include="..\{{projectLayout.ApiClientProjectName}}\{{projectLayout.ApiClientProjectName}}.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Kiota.Abstractions" Version="{{KiotaAbstractionsPackageVersion}}" />
    <PackageReference Include="Microsoft.Kiota.Http.HttpClientLibrary" Version="{{KiotaHttpPackageVersion}}" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="{{MicrosoftExtensionsDependencyInjectionAbstractionsPackageVersion}}" />
    <PackageReference Include="Microsoft.Extensions.Http" Version="{{MicrosoftExtensionsHttpPackageVersion}}" />
  </ItemGroup>

</Project>
""";

        File.WriteAllText(filePath, content);
    }

    private static void CreateSolutionFileWithDotNetCli(
        string solutionRootPath,
        SolutionProjectLayout projectLayout,
        string coreProjectPath,
        string apiClientProjectPath,
        string infrastructureProjectPath)
    {
        Directory.CreateDirectory(solutionRootPath);

        var solutionFilePath = Path.Combine(solutionRootPath, $"{projectLayout.SolutionName}.sln");

        if (File.Exists(solutionFilePath))
            File.Delete(solutionFilePath);

        RunDotNetCommand(
            solutionRootPath,
            ["new", "sln", "--name", projectLayout.SolutionName, "--output", solutionRootPath, "--format", "sln"]);

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
            ["sln", solutionFilePath, "add", Path.Combine(coreProjectPath, $"{projectLayout.ContractsProjectName}.csproj")]);

        RunDotNetCommand(
            solutionRootPath,
            ["sln", solutionFilePath, "add", Path.Combine(apiClientProjectPath, $"{projectLayout.ApiClientProjectName}.csproj")]);

        RunDotNetCommand(
            solutionRootPath,
            ["sln", solutionFilePath, "add", Path.Combine(infrastructureProjectPath, $"{projectLayout.HttpProjectName}.csproj")]);
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

    private static DtoGenerationContext BuildDtoGenerationContext(
        string clientRootPath,
        string baseNamespace,
        IReadOnlyCollection<ApiGroupDefinition> groups,
        KiotaClientMetadata kiotaMetadata)
    {
        var context = new DtoGenerationContext
        {
            BaseNamespace = baseNamespace,
            ClientRootPath = clientRootPath
        };

        foreach (var group in groups)
        {
            foreach (var endpoint in group.Endpoints)
            {
                var operation = ResolveKiotaOperation(
                    group.Name,
                    endpoint,
                    kiotaMetadata,
                    allowCrossMethodPathFallback: false);

                if (operation is null)
                    continue;

                if (!string.IsNullOrWhiteSpace(operation.ReturnTypeName))
                    EnsureDtoTypeRegistered(context, operation.ReturnTypeName);

                if (!string.IsNullOrWhiteSpace(operation.RequestBodyTypeName))
                    EnsureDtoTypeRegistered(context, operation.RequestBodyTypeName);
            }
        }

        return context;
    }

    private static void CreateDtoFiles(
        string coreDtosPath,
        DtoGenerationContext dtoContext)
    {
        foreach (var dtoFile in dtoContext.DtoFiles.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            File.WriteAllText(
                Path.Combine(coreDtosPath, dtoFile.Key),
                dtoFile.Value);
        }
    }

    private static void CreateDtoMapperFile(
        string infrastructureMappersPath,
        SolutionProjectLayout projectLayout)
    {
        var filePath = Path.Combine(infrastructureMappersPath, "GeneratedDtoMapper.cs");

        var content =
$$"""
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;

namespace {{projectLayout.HttpMappersNamespace}};

internal static class GeneratedDtoMapper
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static TTarget? Map<TTarget>(object? source)
    {
        if (source is null)
            return default;

        if (source is TTarget typedTarget)
            return typedTarget;

        var json = JsonSerializer.Serialize(source, Options);
        return JsonSerializer.Deserialize<TTarget>(json, Options);
    }

    public static TTarget MapRequired<TTarget>(object source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source is TTarget typedTarget)
            return typedTarget;

        var json = JsonSerializer.Serialize(source, Options);
        return JsonSerializer.Deserialize<TTarget>(json, Options)
               ?? throw new InvalidOperationException($"Unable to map source to {typeof(TTarget).FullName}.");
    }

    public static List<TTarget>? MapList<TTarget>(IEnumerable? source)
    {
        if (source is null)
            return null;

        var items = new List<TTarget>();

        foreach (var item in source)
        {
            var mapped = Map<TTarget>(item);

            if (mapped is null)
                continue;

            items.Add(mapped);
        }

        return items;
    }
}
""";

        File.WriteAllText(filePath, content);
    }

    private static void EnsureDtoTypeRegistered(
        DtoGenerationContext context,
        string typeName)
    {
        var cleanedTypeName = CleanSourceTypeName(typeName);

        if (string.IsNullOrWhiteSpace(cleanedTypeName))
            return;

        if (IsPrimitiveOrFrameworkType(cleanedTypeName))
            return;

        if (IsKiotaInfrastructureType(cleanedTypeName))
            return;

        if (TryGetGenericTypeDefinition(cleanedTypeName, out _, out var genericArguments))
        {
            foreach (var genericArgument in genericArguments)
                EnsureDtoTypeRegistered(context, genericArgument);

            return;
        }

        var sourceTypeKey = TrimNullableSuffix(cleanedTypeName);

        if (context.SourceToDtoTypeName.ContainsKey(sourceTypeKey))
            return;

        var dtoTypeName = $"{ExtractClassName(sourceTypeKey)}Dto";
        context.SourceToDtoTypeName[sourceTypeKey] = dtoTypeName;

        var sourceTypeFile = FindSourceTypeFile(context.ClientRootPath, sourceTypeKey);

        if (string.IsNullOrWhiteSpace(sourceTypeFile) || !File.Exists(sourceTypeFile))
        {
            context.DtoFiles[$"{dtoTypeName}.cs"] =
$$"""
namespace {{context.BaseNamespace}}.Dtos;

public sealed class {{dtoTypeName}}
{
}
""";
            return;
        }

        var sourceContent = File.ReadAllText(sourceTypeFile);

        if (TryBuildEnumDtoFileContent(context, sourceTypeKey, dtoTypeName, sourceContent, out var enumContent))
        {
            context.ValueTypeDtos.Add(dtoTypeName);
            context.DtoFiles[$"{dtoTypeName}.cs"] = enumContent;
            return;
        }

        context.DtoFiles[$"{dtoTypeName}.cs"] = BuildClassDtoFileContent(
            context,
            sourceTypeKey,
            dtoTypeName,
            sourceContent);
    }

    private static bool TryBuildEnumDtoFileContent(
        DtoGenerationContext context,
        string sourceTypeName,
        string dtoTypeName,
        string sourceContent,
        out string content)
    {
        var match = EnumRegex.Match(sourceContent);

        if (!match.Success)
        {
            content = string.Empty;
            return false;
        }

        var enumBody = match.Groups["body"].Value;
        var enumMembers = ExtractEnumMembers(enumBody);

        if (enumMembers.Length == 0)
        {
            content = string.Empty;
            return false;
        }

        var members = string.Join(
            Environment.NewLine,
            enumMembers.Select(x => $"    {x},"));

        content =
$$"""
namespace {{context.BaseNamespace}}.Dtos;

public enum {{dtoTypeName}}
{
{{members}}
}
""";

        return true;
    }

    private static string[] ExtractEnumMembers(string enumBody)
    {
        if (string.IsNullOrWhiteSpace(enumBody))
            return Array.Empty<string>();

        var result = new List<string>();
        var lines = enumBody.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();

            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (line.StartsWith("[", StringComparison.Ordinal))
                continue;

            if (line.StartsWith("//", StringComparison.Ordinal))
                continue;

            var match = EnumMemberLineRegex.Match(line);

            if (!match.Success)
                continue;

            var memberName = match.Groups["name"].Value.Trim();

            if (string.IsNullOrWhiteSpace(memberName))
                continue;

            result.Add(memberName);
        }

        return result
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string BuildClassDtoFileContent(
        DtoGenerationContext context,
        string sourceTypeName,
        string dtoTypeName,
        string sourceContent)
    {
        var propertyDefinitions = DtoPropertyRegex.Matches(sourceContent)
            .Cast<System.Text.RegularExpressions.Match>()
            .Where(match => match.Success)
            .Select(match => new
            {
                TypeName = CleanSourceTypeName(match.Groups["type"].Value),
                PropertyName = match.Groups["name"].Value.Trim()
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.PropertyName))
            .Where(x => !IsIgnoredRequestBodyProperty(x.PropertyName))
            .GroupBy(x => x.PropertyName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(x => x.PropertyName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var lines = new List<string>();

        foreach (var property in propertyDefinitions)
        {
            var propertyType = MapKiotaTypeToContractType(property.TypeName, context);
            lines.Add($"    public {propertyType} {property.PropertyName} {{ get; set; }}");
        }

        var propertiesBlock = lines.Count == 0
            ? string.Empty
            : string.Join(Environment.NewLine, lines);

        return
$$"""
namespace {{context.BaseNamespace}}.Dtos;

public sealed class {{dtoTypeName}}
{
{{propertiesBlock}}
}
""";
    }

    private static string MapKiotaTypeToContractType(
        string typeName,
        DtoGenerationContext context)
    {
        var cleanedTypeName = CleanSourceTypeName(typeName);

        if (string.IsNullOrWhiteSpace(cleanedTypeName))
            return string.Empty;

        if (IsKiotaInfrastructureType(cleanedTypeName))
            throw new InvalidOperationException($"The Kiota infrastructure type '{cleanedTypeName}' cannot be exposed as a public contract type.");

        if (TryGetGenericTypeDefinition(cleanedTypeName, out var genericTypeName, out var genericArguments))
        {
            var mappedArguments = genericArguments
                .Select(argument => MapKiotaTypeToContractType(argument, context))
                .ToArray();

            var genericTypeClassName = ExtractClassName(genericTypeName);
            var nullableSuffix = HasNullableSuffix(cleanedTypeName) ? "?" : string.Empty;

            if (CollectionTypeNames.Contains(genericTypeClassName, StringComparer.OrdinalIgnoreCase))
                return $"List<{mappedArguments[0]}>".TrimEnd('?') + nullableSuffix;

            return $"{genericTypeClassName}<{string.Join(", ", mappedArguments)}>{nullableSuffix}";
        }

        if (IsPrimitiveOrFrameworkType(cleanedTypeName))
            return NormalizeGeneratedTypeName(cleanedTypeName);

        var sourceTypeKey = TrimNullableSuffix(cleanedTypeName);
        EnsureDtoTypeRegistered(context, sourceTypeKey);

        if (!context.SourceToDtoTypeName.TryGetValue(sourceTypeKey, out var dtoTypeName))
            return NormalizeGeneratedTypeName(cleanedTypeName);

        if (HasNullableSuffix(cleanedTypeName) && !context.ValueTypeDtos.Contains(dtoTypeName))
            return $"{dtoTypeName}?";

        return dtoTypeName;
    }

    private static string? FindSourceTypeFile(
        string clientRootPath,
        string sourceTypeName)
    {
        var className = ExtractClassName(sourceTypeName);

        if (string.IsNullOrWhiteSpace(className))
            return null;

        var matches = Directory.GetFiles(
            clientRootPath,
            $"{className}.cs",
            SearchOption.AllDirectories);

        if (matches.Length == 0)
            return null;

        var namespaceName = ExtractNamespace(sourceTypeName);

        if (string.IsNullOrWhiteSpace(namespaceName))
            return matches[0];

        foreach (var match in matches)
        {
            var content = File.ReadAllText(match);

            if (content.Contains($"namespace {namespaceName};", StringComparison.Ordinal))
                return match;
        }

        return matches[0];
    }

    private static void CreateApiFacadeInterfaceFile(
        string coreInterfacesPath,
        SolutionProjectLayout projectLayout,
        IReadOnlyCollection<ApiGroupDefinition> groups,
        KiotaClientMetadata kiotaMetadata,
        DtoGenerationContext dtoContext,
        AuthenticationMode authenticationMode)
    {
        var filePath = Path.Combine(coreInterfacesPath, "IApiFacade.cs");
        var methodDefinitions = BuildFacadeInterfaceMethods(groups, kiotaMetadata, dtoContext, authenticationMode);

        var content =
$$"""
using System.Threading;
using System.Threading.Tasks;
using {{projectLayout.ContractsDtosNamespace}};

namespace {{projectLayout.ContractsInterfacesNamespace}};

public interface IApiFacade
{
{{methodDefinitions}}
}
""";

        File.WriteAllText(filePath, content);
    }

    private static void CreateAccessTokenAccessorInterfaceFile(
        string coreInterfacesPath,
        SolutionProjectLayout projectLayout)
    {
        var filePath = Path.Combine(coreInterfacesPath, "IAccessTokenAccessor.cs");

        var content =
$$"""
using System.Threading;
using System.Threading.Tasks;

namespace {{projectLayout.ContractsInterfacesNamespace}};

public interface IAccessTokenAccessor
{
    Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default);
}
""";

        File.WriteAllText(filePath, content);
    }

    private static void CreateApiFacadeBaseFile(
        string infrastructureFacadesPath,
        SolutionProjectLayout projectLayout,
        string clientClassName)
    {
        var filePath = Path.Combine(infrastructureFacadesPath, "ApiFacade.cs");

        var fullyQualifiedClientClassName = BuildFullyQualifiedTypeName(
            projectLayout.ApiClientNamespace,
            clientClassName);

        var rootUsing = $"using {projectLayout.ApiClientNamespace};";

        var content =
$$"""
using System;
using System.Net.Http;
{{rootUsing}}
using {{projectLayout.ContractsInterfacesNamespace}};

namespace {{projectLayout.HttpFacadesNamespace}};

public sealed partial class ApiFacade : IApiFacade
{
    private readonly {{fullyQualifiedClientClassName}} _apiClient;
    private readonly IHttpClientFactory _httpClientFactory;

    public ApiFacade(
        {{fullyQualifiedClientClassName}} apiClient,
        IHttpClientFactory httpClientFactory)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    }
}
""";

        File.WriteAllText(filePath, content);
    }

    private static void CreateApiFacadePartialFile(
        string infrastructureFacadesPath,
        SolutionProjectLayout projectLayout,
        ApiGroupDefinition group,
        KiotaClientMetadata kiotaMetadata,
        DtoGenerationContext dtoContext,
        AuthenticationMode authenticationMode)
    {
        var groupName = group.Name.Trim();
        var filePath = Path.Combine(infrastructureFacadesPath, $"ApiFacade.{groupName}.cs");
        var methods = BuildFacadePartialMethods(group, kiotaMetadata, dtoContext, authenticationMode);

        var content =
$$"""
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Kiota.Abstractions;
using {{projectLayout.ContractsDtosNamespace}};
using {{projectLayout.HttpMappersNamespace}};

namespace {{projectLayout.HttpFacadesNamespace}};

public sealed partial class ApiFacade
{
{{methods}}
}
""";

        File.WriteAllText(filePath, content);
    }

    private static void CreateServiceInterfaceFile(
        string coreInterfacesPath,
        SolutionProjectLayout projectLayout,
        ApiGroupDefinition group,
        KiotaClientMetadata kiotaMetadata,
        DtoGenerationContext dtoContext,
        AuthenticationMode authenticationMode)
    {
        var groupName = group.Name.Trim();
        var filePath = Path.Combine(coreInterfacesPath, $"I{groupName}Service.cs");
        var methods = BuildServiceInterfaceMethods(group, kiotaMetadata, dtoContext, authenticationMode);

        var content =
$$"""
using System.Threading;
using System.Threading.Tasks;
using {{projectLayout.ContractsDtosNamespace}};

namespace {{projectLayout.ContractsInterfacesNamespace}};

public interface I{{groupName}}Service
{
{{methods}}
}
""";

        File.WriteAllText(filePath, content);
    }

    private static void CreateServiceImplementationFile(
        string infrastructureServicesPath,
        SolutionProjectLayout projectLayout,
        ApiGroupDefinition group,
        KiotaClientMetadata kiotaMetadata,
        DtoGenerationContext dtoContext,
        AuthenticationMode authenticationMode)
    {
        var groupName = group.Name.Trim();
        var filePath = Path.Combine(infrastructureServicesPath, $"{groupName}Service.cs");
        var methods = BuildServiceImplementationMethods(group, kiotaMetadata, dtoContext, authenticationMode);

        var content =
$$"""
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Kiota.Abstractions;
using {{projectLayout.ContractsDtosNamespace}};
using {{projectLayout.ContractsInterfacesNamespace}};

namespace {{projectLayout.HttpServicesNamespace}};

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

private static void CreateServiceCollectionExtensionsFile(
    string infrastructureDependencyInjectionPath,
    SolutionProjectLayout projectLayout,
    IReadOnlyCollection<ApiGroupDefinition> groups,
    string clientClassName,
    AuthenticationMode authenticationMode)
{
        var filePath = Path.Combine(infrastructureDependencyInjectionPath, "ServiceCollectionExtensions.cs");

        var serviceRegistrations = groups.Count == 0
            ? "        // No API groups were detected."
            : string.Join(Environment.NewLine, groups.Select(g => $"        services.AddScoped<I{g.Name}Service, {g.Name}Service>();"));

        var authenticationUsing = authenticationMode == AuthenticationMode.AccessTokenAccessor
            ? $"using {projectLayout.HttpAuthenticationNamespace};"
            : string.Empty;

        // O modo de autenticação altera o contrato público e também a forma como o Kiota recebe o token em runtime.
        var authenticationRegistration = authenticationMode == AuthenticationMode.AccessTokenAccessor
            ? "        services.AddScoped<IAuthenticationProvider, AccessTokenAuthenticationProvider>();"
            : "        services.AddScoped<IAuthenticationProvider, AnonymousAuthenticationProvider>();";

        var content =
    $$"""
using System;
using {{projectLayout.ApiClientNamespace}};
using {{projectLayout.ContractsInterfacesNamespace}};
using {{projectLayout.HttpFacadesNamespace}};
using {{projectLayout.HttpServicesNamespace}};
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
{{authenticationUsing}}

namespace {{projectLayout.HttpDependencyInjectionNamespace}};

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGeneratedApi(
        this IServiceCollection services,
        string baseUrl)
    {
        if (services is null)
            throw new ArgumentNullException(nameof(services));

        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new ArgumentException("Base URL cannot be empty.", nameof(baseUrl));

        services.AddHttpClient("GeneratedApi", client =>
        {
            client.BaseAddress = new Uri(baseUrl);
        });

{{authenticationRegistration}}

        services.AddScoped<IRequestAdapter>(serviceProvider =>
        {
            var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient("GeneratedApi");
            var authenticationProvider = serviceProvider.GetRequiredService<IAuthenticationProvider>();

            return new HttpClientRequestAdapter(authenticationProvider, httpClient: httpClient)
            {
                BaseUrl = baseUrl
            };
        });

        services.AddScoped<{{clientClassName}}>(serviceProvider =>
        {
            var requestAdapter = serviceProvider.GetRequiredService<IRequestAdapter>();
            return new {{clientClassName}}(requestAdapter);
        });

        services.AddScoped<IApiFacade, ApiFacade>();

{{serviceRegistrations}}

        return services;
    }
}
""";

        File.WriteAllText(filePath, content);
    }

    private static void CreateReadmeFile(
        string solutionRootPath,
        ModernizationRequest request,
        SolutionProjectLayout projectLayout,
        IReadOnlyCollection<ApiGroupDefinition> groups)
    {
        var projectName = request.ProjectName.ToString();
        var baseNamespace = request.BaseNamespace.ToString();

        var groupsSection = groups.Count == 0
            ? "- No API groups were detected during composition."
            : string.Join(Environment.NewLine, groups.Select(g => $"- `{g.Name}` ({g.Endpoints.Count} endpoints)"));

        var filePath = Path.Combine(solutionRootPath, "README.md");

        var structureSection = request.GenerationMode == GenerationMode.Embedded
            ? $$"""
## Embedded Structure

- `{{projectLayout.ContractsProjectName}}`
- `{{projectLayout.ApiClientProjectName}}`
- `{{projectLayout.HttpProjectName}}`

This output was generated for immediate incorporation into an existing solution.
See `INTEGRATION.md` for the recommended setup.
"""
            : """
## Standalone Structure

This output was generated as an autonomous solution and can be evaluated in isolation.
""";

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

{{structureSection}}

## Notes

The generated facade follows the partial class pattern, allowing each API area to be maintained in a separate file.
""";

        File.WriteAllText(filePath, content);
    }

    private static void CreateAccessTokenAuthenticationProviderFile(
        string infrastructureAuthenticationPath,
        SolutionProjectLayout projectLayout)
    {
        var filePath = Path.Combine(infrastructureAuthenticationPath, "AccessTokenAuthenticationProvider.cs");

        var content =
$$"""
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using {{projectLayout.ContractsInterfacesNamespace}};
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;

namespace {{projectLayout.HttpAuthenticationNamespace}};

internal sealed class AccessTokenAuthenticationProvider : IAuthenticationProvider
{
    private readonly IAccessTokenAccessor _accessTokenAccessor;

    public AccessTokenAuthenticationProvider(IAccessTokenAccessor accessTokenAccessor)
    {
        _accessTokenAccessor = accessTokenAccessor ?? throw new ArgumentNullException(nameof(accessTokenAccessor));
    }

    public async Task AuthenticateRequestAsync(
        RequestInformation request,
        Dictionary<string, object>? additionalAuthenticationContext = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var accessToken = await _accessTokenAccessor.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(accessToken))
            return;

        request.Headers.TryAdd("Authorization", $"Bearer {accessToken}");
    }
}
""";

        File.WriteAllText(filePath, content);
    }

    private static void CreateGenerationManifestFile(
        string solutionRootPath,
        ModernizationRequest request,
        SolutionProjectLayout projectLayout,
        IReadOnlyCollection<ApiGroupDefinition> groups,
        KiotaClientMetadata kiotaMetadata,
        DtoGenerationContext dtoContext)
    {
        var filePath = Path.Combine(solutionRootPath, "generation-manifest.json");

        // O manifesto registra como a geração resolveu tipos, projetos e contratos,
        // servindo como resumo técnico da execução.
        var manifest = new
        {
            projectName = request.ProjectName.ToString(),
            baseNamespace = request.BaseNamespace.ToString(),
            generationMode = request.GenerationMode.ToString(),
            authenticationMode = request.AuthenticationMode.ToString(),
            targetFramework = request.TargetFramework,
            solutionName = projectLayout.SolutionName,
            projects = new
            {
                contracts = projectLayout.ContractsProjectName,
                apiClient = projectLayout.ApiClientProjectName,
                http = projectLayout.HttpProjectName
            },
            namespaces = new
            {
                contracts = projectLayout.ContractsNamespace,
                apiClient = projectLayout.ApiClientNamespace,
                http = projectLayout.HttpNamespace
            },
            generatedClientRootNamespace = kiotaMetadata.RootNamespace,
            generatedClientClassName = kiotaMetadata.ClientClassName,
            dtoMappings = dtoContext.SourceToDtoTypeName
                .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .Select(x => new
                {
                    sourceType = x.Key,
                    dtoType = x.Value
                })
                .ToArray(),
            groups = groups
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .Select(group => BuildManifestGroup(group, kiotaMetadata, dtoContext))
                .ToArray()
        };

        var content = System.Text.Json.JsonSerializer.Serialize(
            manifest,
            new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });

        File.WriteAllText(filePath, content);
    }

    private static void CreateIntegrationManifestFile(
        string solutionRootPath,
        ModernizationRequest request,
        SolutionProjectLayout projectLayout,
        IReadOnlyCollection<ApiGroupDefinition> groups)
    {
        var filePath = Path.Combine(solutionRootPath, "integration-manifest.json");

        // No modo Embedded a aplicação hospedeira precisa de um mapa simples de como plugar o módulo gerado.
        var manifest = new
        {
            generationMode = request.GenerationMode.ToString(),
            authenticationMode = request.AuthenticationMode.ToString(),
            projectPrefix = request.EmbeddedProjectPrefix?.ToString(),
            projects = new
            {
                contracts = projectLayout.ContractsProjectName,
                apiClient = projectLayout.ApiClientProjectName,
                http = projectLayout.HttpProjectName
            },
            namespaces = new
            {
                contracts = projectLayout.ContractsNamespace,
                apiClient = projectLayout.ApiClientNamespace,
                http = projectLayout.HttpNamespace
            },
            entrypoints = new
            {
                serviceCollectionExtension = $"{projectLayout.HttpDependencyInjectionNamespace}.ServiceCollectionExtensions",
                facadeInterface = $"{projectLayout.ContractsInterfacesNamespace}.IApiFacade",
                accessTokenAccessorInterface = request.AuthenticationMode == AuthenticationMode.AccessTokenAccessor
                    ? $"{projectLayout.ContractsInterfacesNamespace}.IAccessTokenAccessor"
                    : null
            },
            consumerGuidance = new
            {
                suggestedReference = projectLayout.HttpProjectName,
                addGeneratedApiMethod = "AddGeneratedApi",
                requiresAccessTokenAccessor = request.AuthenticationMode == AuthenticationMode.AccessTokenAccessor,
                apiGroupServices = groups
                    .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(x => $"I{x.Name}Service")
                    .ToArray()
            }
        };

        var content = System.Text.Json.JsonSerializer.Serialize(
            manifest,
            new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });

        File.WriteAllText(filePath, content);
    }

    private static void CreateIntegrationGuideFile(
        string solutionRootPath,
        ModernizationRequest request,
        SolutionProjectLayout projectLayout,
        IReadOnlyCollection<ApiGroupDefinition> groups)
    {
        var filePath = Path.Combine(solutionRootPath, "INTEGRATION.md");

        var servicesSection = groups.Count == 0
            ? "- No grouped services were generated."
            : string.Join(Environment.NewLine, groups
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .Select(x => $"- `I{x.Name}Service` / `{x.Name}Service`"));

        var accessTokenAccessorLine = request.AuthenticationMode == AuthenticationMode.AccessTokenAccessor
            ? $"- `{projectLayout.ContractsInterfacesNamespace}.IAccessTokenAccessor`{Environment.NewLine}"
            : string.Empty;

        var configurationKey = $"Apis:{request.EmbeddedProjectPrefix}:BaseUrl";
        var suggestedApiBaseUrl = ResolveSuggestedApiBaseUrl(request.SpecificationSource);
        var sampleHostProjectName = $"{request.EmbeddedProjectPrefix}.Web";
        var sampleAccessTokenNamespace = $"{request.EmbeddedProjectPrefix}.Web.Security";
        var authenticationRegistrationSection = request.AuthenticationMode == AuthenticationMode.AccessTokenAccessor
            ? $$"""
## Authentication Setup

This output was generated with `AccessTokenAccessor` mode. That means the host application must provide an implementation of:

- `{{projectLayout.ContractsInterfacesNamespace}}.IAccessTokenAccessor`

Create this implementation inside the consuming application project, not inside the generated projects.
Good locations:

- `src/{{request.EmbeddedProjectPrefix}}.Admin/Security/AccessTokenAccessor.cs`
- `src\{{sampleHostProjectName}}\Security\AccessTokenAccessor.cs`
- `src/{{request.EmbeddedProjectPrefix}}.App/Authentication/AccessTokenAccessor.cs`

Example file:

- `src\{{sampleHostProjectName}}\Security\AccessTokenAccessor.cs`

```csharp
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using {{projectLayout.ContractsInterfacesNamespace}};

namespace {{sampleAccessTokenNamespace}};

public sealed class AccessTokenAccessor(IHttpContextAccessor httpContextAccessor) : IAccessTokenAccessor
{
    public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var token = httpContextAccessor.HttpContext?.Session.GetString("access_token");
        return Task.FromResult(token);
    }
}
```

If your application stores the token in another place, adapt only this class. The generated module does not need to change.
"""
            : """
## Authentication Setup

This output was generated with `PerMethodToken` mode.
That means the consuming application can pass the bearer token directly into the generated methods that require authorization.

In this mode, no access token accessor implementation is required.
""";

        var content =
$$"""
# Integration Guide

This package was generated in `Embedded` mode to be copied into an existing solution and used as a client module for an existing API.

The goal of this guide is to explain, step by step, how to place the projects correctly, how to reference them, how to register them in dependency injection, and how to validate the final integration.

## Quick Integration Checklist

Use this checklist when integrating the generated module into the host solution:

1. Copy the 3 generated folders into the host solution `src` folder.
2. Keep the 3 generated projects as siblings under the same parent folder.
3. Add the 3 `.csproj` files to the host `.sln`.
4. Run `dotnet restore`.
5. Add a project reference from the host application to `{{projectLayout.HttpProjectName}}`.
6. Configure the API base URL using `{{configurationKey}}`.
7. If the module was generated with `AccessTokenAccessor`, create and register `IAccessTokenAccessor`.
8. Register `AddGeneratedApi(baseUrl)` in `Program.cs` or `Startup.cs`.
9. Run `dotnet build`.
10. Consume only the generated contracts and services.

## Generated Projects

- `{{projectLayout.ContractsProjectName}}`
- `{{projectLayout.ApiClientProjectName}}`
- `{{projectLayout.HttpProjectName}}`

These projects are usually located inside the generated package under:

```text
src/
  {{projectLayout.ContractsProjectName}}
  {{projectLayout.ApiClientProjectName}}
  {{projectLayout.HttpProjectName}}
```

## Before You Start

Before adding these projects into your host solution, check the points below:

1. The three generated projects must stay together under the same parent folder.
2. Do not rename the folders or `.csproj` files before the first successful build.
3. Do not add only one or two projects. The three projects are required together.
4. The consuming application should reference only `{{projectLayout.HttpProjectName}}`.
5. `{{projectLayout.HttpProjectName}}` already contains direct `ProjectReference` entries to `{{projectLayout.ContractsProjectName}}` and `{{projectLayout.ApiClientProjectName}}`.
6. If you move only one of the generated folders, the internal `ProjectReference` paths will break and the host solution will stop compiling.

## Purpose Of Each Project

### `{{projectLayout.ContractsProjectName}}`

Contains the public contracts that the host application should reference directly:

- DTOs
- `IApiFacade`
{{accessTokenAccessorLine}}{{servicesSection}}

### `{{projectLayout.ApiClientProjectName}}`

Contains the raw `Kiota` client generated from the OpenAPI specification.

This project exists as a technical dependency and should not be consumed directly by pages, controllers, view models or application use cases.

### `{{projectLayout.HttpProjectName}}`

Contains the implementation layer:

- facades
- services
- mappers
- dependency injection bootstrap
- generated authentication support

## Recommended Folder Layout Inside The Host Solution

The easiest and safest integration is to place the generated projects as siblings of the existing host projects.

Example:

```text
src/
  {{sampleHostProjectName}}
  {{projectLayout.ContractsProjectName}}
  {{projectLayout.ApiClientProjectName}}
  {{projectLayout.HttpProjectName}}
```

If you place the generated projects in a different folder structure, the relative `ProjectReference` paths inside the generated `.csproj` files may no longer be valid. In that case you must manually adjust the `ProjectReference Include="..."` entries.

## Step 1 - Copy The Generated Projects To The Host Solution

Copy the following folders from the generated package into the `src` folder of the host solution:

- `src/{{projectLayout.ContractsProjectName}}`
- `src/{{projectLayout.ApiClientProjectName}}`
- `src/{{projectLayout.HttpProjectName}}`

Expected result:

```text
YourHostSolution/
  src/
    {{sampleHostProjectName}}/
    {{projectLayout.ContractsProjectName}}/
    {{projectLayout.ApiClientProjectName}}/
    {{projectLayout.HttpProjectName}}/
```

Only after the folders are copied to their final place should you add them to the `.sln`.

## Step 2 - Add The Generated Projects To The Solution

The generated projects already include explicit package versions and opt out of central package version management in `Embedded` mode.
This is intentional, so the imported module can compile more predictably even when the host solution has its own package policy.

Open a terminal in the root folder of the host solution and run:

```powershell
dotnet sln add .\src\{{projectLayout.ContractsProjectName}}\{{projectLayout.ContractsProjectName}}.csproj
dotnet sln add .\src\{{projectLayout.ApiClientProjectName}}\{{projectLayout.ApiClientProjectName}}.csproj
dotnet sln add .\src\{{projectLayout.HttpProjectName}}\{{projectLayout.HttpProjectName}}.csproj
```

If you prefer, you can also add them through Visual Studio:

1. Right click the solution.
2. Choose `Add > Existing Project`.
3. Select the three generated `.csproj` files.

### Important note about packages

When these projects are copied into another solution, the package references should remain explicit inside the generated `.csproj` files.
If your host solution enforces a custom package policy, review these versions before changing them:

- `Microsoft.Kiota.*`
- `Microsoft.Extensions.DependencyInjection.Abstractions`
- `Microsoft.Extensions.Http`

If the host solution uses `Directory.Packages.props` or another central package strategy, do not change the generated projects before the first successful restore and build.

## Step 3 - Restore Packages

After adding the projects to the solution, run:

```powershell
dotnet restore
```

This step helps surface package conflicts before the first full build.

## Step 4 - Reference The HTTP Project From The Consuming Application

The host application usually needs to reference only the HTTP project directly, because it already depends on `Contracts` and `ApiClient`.

Typical consuming applications:

- MVC / Razor project
- Admin dashboard
- Web API that orchestrates another API
- MAUI host project
- Blazor host project

If the host project is `src/{{sampleHostProjectName}}\{{sampleHostProjectName}}.csproj`, run:

```powershell
dotnet add .\src\{{sampleHostProjectName}}\{{sampleHostProjectName}}.csproj reference .\src\{{projectLayout.HttpProjectName}}\{{projectLayout.HttpProjectName}}.csproj
```

Important:

- add the reference to `{{projectLayout.HttpProjectName}}`
- do not add a direct reference from the host project to `{{projectLayout.ApiClientProjectName}}`
- do not inject Kiota builders directly into controllers, pages or use cases

## Step 5 - Configure The API Base URL

Add the API base URL to the host application's configuration.

Suggested API base URL captured during generation:

```text
{{suggestedApiBaseUrl}}
```

This value was inferred from the specification source informed in the LMT.
If the specification URL was, for example, a Swagger or OpenAPI endpoint such as:

- `https://localhost:7054/swagger/v1/swagger.json`
- `https://api.company.com/swagger/v1/swagger.json`
- `https://api.company.com/my-app/openapi/v1.json`

the guide tries to suggest the base API URL that the host application should call.
Always review this value before finishing the integration, especially when the API is hosted behind a gateway, reverse proxy or virtual directory.

Example file:

- `src\{{sampleHostProjectName}}\appsettings.json`

Example section:

```json
{
  "Apis": {
    "{{request.EmbeddedProjectPrefix}}": {
      "BaseUrl": "{{suggestedApiBaseUrl}}"
    }
  }
}
```

Configuration key used in the sample below:

- `{{configurationKey}}`

## Step 6 - Register The Generated Module In Dependency Injection

In the startup file of the host application, register the generated module using:

- `{{projectLayout.HttpDependencyInjectionNamespace}}.ServiceCollectionExtensions`
- method: `AddGeneratedApi(baseUrl)`

Typical files:

- `Program.cs`
- `Startup.cs`

Minimal registration example:

```csharp
using {{projectLayout.HttpDependencyInjectionNamespace}};

builder.Services.AddGeneratedApi("{{suggestedApiBaseUrl}}");
```

If your API base URL comes from configuration, prefer something like:

```csharp
using {{projectLayout.HttpDependencyInjectionNamespace}};

var apiBaseUrl = builder.Configuration["{{configurationKey}}"]
                 ?? throw new InvalidOperationException("API base URL was not configured.");

builder.Services.AddGeneratedApi(apiBaseUrl);
```

{{authenticationRegistrationSection}}

## Step 7 - Full Program.cs Example

The example below shows a complete `Program.cs` for an ASP.NET Core MVC or Razor application using:

- session to store the access token
- `IAccessTokenAccessor`
- `AddGeneratedApi(baseUrl)`
- MVC controllers and views

Example file:

- `src\{{sampleHostProjectName}}\Program.cs`

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using {{projectLayout.ContractsInterfacesNamespace}};
using {{projectLayout.HttpDependencyInjectionNamespace}};
using {{sampleAccessTokenNamespace}};

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromMinutes(60);
});

var apiBaseUrl = builder.Configuration["{{configurationKey}}"]
                 ?? throw new InvalidOperationException("The API base URL was not configured.");

builder.Services.AddScoped<IAccessTokenAccessor, AccessTokenAccessor>();
builder.Services.AddGeneratedApi(apiBaseUrl);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
```

If your team prefers to keep `AccessTokenAccessor` in its own file, keep the class in `Security/AccessTokenAccessor.cs` and leave `Program.cs` only with the DI registration line:

```csharp
builder.Services.AddScoped<IAccessTokenAccessor, AccessTokenAccessor>();
```

## Step 8 - Consume Only The Generated Contracts And Services

After registration, the consuming application should request dependencies from DI using the generated interfaces.

Prefer using:

- `{{projectLayout.ContractsInterfacesNamespace}}.IApiFacade`
{{accessTokenAccessorLine}}{{servicesSection}}

Do not consume:

- raw Kiota request builders
- Kiota models directly in pages or controllers
- `{{projectLayout.ApiClientProjectName}}` as a public API surface

## Step 9 - Example Of Consumption In The Host Application

### Example in a controller or page model

```csharp
using Microsoft.AspNetCore.Mvc;
using {{projectLayout.ContractsInterfacesNamespace}};

public sealed class DashboardController : Controller
{
    private readonly IAuthenticationService _authenticationService;

    public DashboardController(IAuthenticationService authenticationService)
    {
        _authenticationService = authenticationService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var result = await _authenticationService.GETAuthenticationMeAsync(cancellationToken);
        return View(result);
    }
}
```

### Important rule

The host application should depend on service and facade contracts, not on Kiota internals.

## Step 10 - Validate The Integration

Run the commands below in the root of the host solution:

```powershell
dotnet restore
dotnet build
```

If the build succeeds, the module is structurally integrated.

If the host application already has tests, also run:

```powershell
dotnet test
```

If you want a quick smoke test after build:

```powershell
dotnet run --project .\src\{{sampleHostProjectName}}\{{sampleHostProjectName}}.csproj
```

## Main Contracts

- `{{projectLayout.ContractsInterfacesNamespace}}.IApiFacade`
{{accessTokenAccessorLine}}{{servicesSection}}

## Troubleshooting

If the integration does not work as expected, verify these points first:

1. The three generated folders were copied as siblings under the same parent folder.
2. The three generated projects were added to the target solution.
3. The consuming application references `{{projectLayout.HttpProjectName}}`.
4. `dotnet restore` completed successfully after the import.
5. `AddGeneratedApi(baseUrl)` was registered in startup.
6. The API base URL is correct and reachable.
7. If using `AccessTokenAccessor`, the implementation is registered in DI.
8. The host application is consuming `Contracts` interfaces instead of Kiota classes.
9. No one manually edited the generated `.csproj` paths before the first successful build.

## Naming Convention

This module follows the embedded naming convention:

- `{{request.EmbeddedProjectPrefix}}.Lmt.Application.Contracts`
- `{{request.EmbeddedProjectPrefix}}.Lmt.Application.ApiClient`
- `{{request.EmbeddedProjectPrefix}}.Lmt.Application.Http`
""";

        File.WriteAllText(filePath, content);
    }

    private static string ResolveSuggestedApiBaseUrl(SpecificationSource specificationSource)
    {
        if (specificationSource.Type != SpecificationSourceType.Url)
            return "https://api.example.com";

        if (!Uri.TryCreate(specificationSource.Value, UriKind.Absolute, out var uri))
            return "https://api.example.com";

        var authority = uri.GetLeftPart(UriPartial.Authority);
        var path = uri.AbsolutePath.Replace('\\', '/');

        if (string.IsNullOrWhiteSpace(path) || path == "/")
            return authority;

        var normalizedPath = path.TrimEnd('/');
        var swaggerIndex = normalizedPath.IndexOf("/swagger", StringComparison.OrdinalIgnoreCase);

        if (swaggerIndex >= 0)
        {
            var basePathBeforeSwagger = normalizedPath[..swaggerIndex];
            return CombineBaseUrl(authority, basePathBeforeSwagger);
        }

        var openApiIndex = normalizedPath.IndexOf("/openapi", StringComparison.OrdinalIgnoreCase);

        if (openApiIndex >= 0)
        {
            var basePathBeforeOpenApi = normalizedPath[..openApiIndex];
            return CombineBaseUrl(authority, basePathBeforeOpenApi);
        }

        var segments = normalizedPath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (segments.Length == 0)
            return authority;

        var lastSegment = segments[^1];

        if (lastSegment.Contains('.', StringComparison.Ordinal))
        {
            var baseSegments = segments.Take(segments.Length - 1).ToArray();
            var basePath = baseSegments.Length == 0
                ? string.Empty
                : "/" + string.Join("/", baseSegments);

            return CombineBaseUrl(authority, basePath);
        }

        return CombineBaseUrl(authority, normalizedPath);
    }

    private static string CombineBaseUrl(string authority, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path == "/")
            return authority;

        return $"{authority}{path}";
    }

    private static object BuildManifestGroup(
        ApiGroupDefinition group,
        KiotaClientMetadata kiotaMetadata,
        DtoGenerationContext dtoContext)
    {
        return new
        {
            name = group.Name,
            endpoints = group.Endpoints
                .OrderBy(x => x.Method, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
                .Select(endpoint => BuildManifestEndpoint(group.Name, endpoint, kiotaMetadata, dtoContext))
                .ToArray()
        };
    }

    private static object BuildManifestEndpoint(
        string groupName,
        ApiEndpointDefinition endpoint,
        KiotaClientMetadata kiotaMetadata,
        DtoGenerationContext dtoContext)
    {
        var operation = ResolveKiotaOperation(
            groupName,
            endpoint,
            kiotaMetadata,
            allowCrossMethodPathFallback: false);

        return new
        {
            method = endpoint.Method,
            path = endpoint.Path,
            operationId = endpoint.OperationId,
            generatedMethodName = NormalizeMethodName(endpoint.OperationId, endpoint.Path, endpoint.Method),
            requiresAuthorization = endpoint.RequiresAuthorization,
            hasRequestBody = endpoint.HasRequestBody,
            kiota = operation is null
                ? null
                : new
                {
                    methodName = operation.MethodName,
                    httpMethod = operation.HttpMethod,
                    endpointPath = operation.EndpointPath,
                    accessExpression = operation.AccessExpression,
                    returnTypeName = operation.ReturnTypeName,
                    requestBodyTypeName = operation.RequestBodyTypeName,
                    isCollection = operation.IsCollection,
                    isCollectionWrapper = operation.IsCollectionWrapper,
                    collectionPropertyName = operation.CollectionPropertyName,
                    pathParameters = operation.PathParameters
                        .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                        .Select(x => new
                        {
                            name = x.Name,
                            typeName = x.TypeName,
                            accessExpression = x.AccessExpression
                        })
                        .ToArray()
                },
            contracts = new
            {
                returnType = ResolveContractReturnType(groupName, endpoint, kiotaMetadata, dtoContext),
                requestBodyType = endpoint.HasRequestBody
                    ? ResolveContractRequestBodyType(groupName, endpoint, kiotaMetadata, dtoContext)
                    : string.Empty
            },
            parameters = endpoint.Parameters
                .OrderBy(x => x.Location, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .Select(x => new
                {
                    name = x.Name,
                    location = x.Location,
                    required = x.Required,
                    schemaType = x.SchemaType,
                    schemaFormat = x.SchemaFormat
                })
                .ToArray()
        };
    }

    private static string BuildFacadeInterfaceMethods(
        IReadOnlyCollection<ApiGroupDefinition> groups,
        KiotaClientMetadata kiotaMetadata,
        DtoGenerationContext dtoContext,
        AuthenticationMode authenticationMode)
    {
        var methods = groups
            .SelectMany(g => g.Endpoints.Select(e => new
            {
                Group = g,
                Endpoint = e,
                MethodName = NormalizeMethodName(e.OperationId, e.Path, e.Method)
            }))
            .Where(x => !string.IsNullOrWhiteSpace(x.MethodName))
            .GroupBy(x => x.MethodName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(x => x.MethodName)
            .Select(x =>
            {
                var returnType = ResolveContractReturnType(x.Group.Name, x.Endpoint, kiotaMetadata, dtoContext);
                var parameters = BuildFacadeMethodParameters(x.Group.Name, x.Endpoint, kiotaMetadata, dtoContext, authenticationMode);
                var taskType = BuildTaskReturnType(returnType);

                return $"    {taskType} {x.MethodName}Async({parameters});";
            });

        return string.Join(Environment.NewLine + Environment.NewLine, methods);
    }

    private static string BuildFacadePartialMethods(
    ApiGroupDefinition group,
    KiotaClientMetadata kiotaMetadata,
    DtoGenerationContext dtoContext,
    AuthenticationMode authenticationMode)
    {
        var methods = group.Endpoints
            .Select(e => new
            {
                Endpoint = e,
                MethodName = NormalizeMethodName(e.OperationId, e.Path, e.Method)
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.MethodName))
            .GroupBy(x => x.MethodName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(x => x.MethodName)
            .Select(x =>
            {
                var parameters = BuildFacadeMethodParameters(group.Name, x.Endpoint, kiotaMetadata, dtoContext, authenticationMode);
                var returnType = ResolveContractReturnType(group.Name, x.Endpoint, kiotaMetadata, dtoContext);
                var kiotaRequestBodyType = x.Endpoint.HasRequestBody
                    ? ResolveRequestBodyType(group.Name, x.Endpoint, kiotaMetadata)
                    : string.Empty;
                var operation = ResolveKiotaOperation(
                    group.Name,
                    x.Endpoint,
                    kiotaMetadata,
                    allowCrossMethodPathFallback: false);
                var kiotaCallExpression = BuildKiotaCallExpression(
                    group.Name,
                    x.Endpoint,
                    kiotaMetadata,
                    string.IsNullOrWhiteSpace(kiotaRequestBodyType) ? "request" : "kiotaRequest",
                    authenticationMode);
                var taskType = BuildTaskReturnType(returnType);
                var methodBody = BuildFacadeMethodBody(returnType, operation, kiotaCallExpression, kiotaRequestBodyType);

                return
    $$"""
    public async {{taskType}} {{x.MethodName}}Async({{parameters}})
    {
{{methodBody}}
    }
""";
            });

        return string.Join(Environment.NewLine + Environment.NewLine, methods);
    }

    private static string BuildServiceInterfaceMethods(
        ApiGroupDefinition group,
        KiotaClientMetadata kiotaMetadata,
        DtoGenerationContext dtoContext,
        AuthenticationMode authenticationMode)
    {
        var methods = group.Endpoints
            .Select(e => new
            {
                Endpoint = e,
                MethodName = NormalizeMethodName(e.OperationId, e.Path, e.Method)
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.MethodName))
            .GroupBy(x => x.MethodName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(x => x.MethodName)
            .Select(x =>
            {
                var returnType = ResolveContractReturnType(group.Name, x.Endpoint, kiotaMetadata, dtoContext);
                var parameters = BuildServiceMethodParameters(group.Name, x.Endpoint, kiotaMetadata, dtoContext, authenticationMode);
                var taskType = BuildTaskReturnType(returnType);

                return $"    {taskType} {x.MethodName}Async({parameters});";
            });

        return string.Join(Environment.NewLine + Environment.NewLine, methods);
    }

    private static string BuildServiceImplementationMethods(
        ApiGroupDefinition group,
        KiotaClientMetadata kiotaMetadata,
        DtoGenerationContext dtoContext,
        AuthenticationMode authenticationMode)
    {
        var methods = group.Endpoints
            .Select(e => new
            {
                Endpoint = e,
                MethodName = NormalizeMethodName(e.OperationId, e.Path, e.Method)
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.MethodName))
            .GroupBy(x => x.MethodName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(x => x.MethodName)
            .Select(x =>
            {
                var parameters = BuildServiceMethodParameters(group.Name, x.Endpoint, kiotaMetadata, dtoContext, authenticationMode);
                var returnType = ResolveContractReturnType(group.Name, x.Endpoint, kiotaMetadata, dtoContext);
                var operation = ResolveKiotaOperation(
                    group.Name,
                    x.Endpoint,
                    kiotaMetadata,
                    allowCrossMethodPathFallback: false);
                var requestCreationBlock = BuildRequestBodyCreationBlock(operation);
                var facadeArguments = BuildFacadeArgumentsFromService(group.Name, x.Endpoint, kiotaMetadata, authenticationMode);
                var taskType = BuildTaskReturnType(returnType);

                return
$$"""
    public {{taskType}} {{x.MethodName}}Async({{parameters}})
    {
{{requestCreationBlock}}        return _apiFacade.{{x.MethodName}}Async({{facadeArguments}});
    }
""";
            });

        return string.Join(Environment.NewLine + Environment.NewLine, methods);
    }

    private static string BuildFacadeMethodParameters(
        string groupName,
        ApiEndpointDefinition endpoint,
        KiotaClientMetadata kiotaMetadata,
        DtoGenerationContext dtoContext,
        AuthenticationMode authenticationMode)
    {
        var parameters = new List<string>();

        AddPathParameters(parameters, groupName, endpoint, kiotaMetadata);

        if (endpoint.HasRequestBody)
        {
            var bodyType = ResolveContractRequestBodyType(groupName, endpoint, kiotaMetadata, dtoContext);
            parameters.Add($"{bodyType} request");
        }

        AddQueryParameters(parameters, endpoint);
        AddHeaderParameters(parameters, endpoint);

        // No modo AccessTokenAccessor o token deixa de fazer parte da assinatura pública.
        if (endpoint.RequiresAuthorization && authenticationMode == AuthenticationMode.PerMethodToken)
            parameters.Add("string? accessToken = null");

        parameters.Add("CancellationToken cancellationToken = default");

        return string.Join(", ", parameters);
    }

    private static string BuildServiceMethodParameters(
        string groupName,
        ApiEndpointDefinition endpoint,
        KiotaClientMetadata kiotaMetadata,
        DtoGenerationContext dtoContext,
        AuthenticationMode authenticationMode)
    {
        var parameters = new List<string>();

        AddPathParameters(parameters, groupName, endpoint, kiotaMetadata);

        if (endpoint.HasRequestBody)
        {
            var bodyType = ResolveContractRequestBodyType(groupName, endpoint, kiotaMetadata, dtoContext);
            parameters.Add($"{bodyType} request");
        }

        AddQueryParameters(parameters, endpoint);
        AddHeaderParameters(parameters, endpoint);

        if (endpoint.RequiresAuthorization && authenticationMode == AuthenticationMode.PerMethodToken)
            parameters.Add("string? accessToken = null");

        parameters.Add("CancellationToken cancellationToken = default");

        return string.Join(", ", parameters);
    }

    private static bool IsOperationSafeForExplodedRequest(
    ApiEndpointDefinition endpoint,
    KiotaOperationMetadata operation)
    {
        if (operation is null)
            return false;

        if (string.IsNullOrWhiteSpace(operation.RequestBodyTypeName))
            return false;

        if (operation.RequestBodyProperties.Count == 0)
            return false;

        var requestBodyClassName = ExtractClassName(operation.RequestBodyTypeName);

        if (requestBodyClassName.EndsWith("PostRequestBody", StringComparison.OrdinalIgnoreCase) ||
            requestBodyClassName.EndsWith("PutRequestBody", StringComparison.OrdinalIgnoreCase) ||
            requestBodyClassName.EndsWith("PatchRequestBody", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static string ExtractClassName(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return string.Empty;

        var cleaned = typeName
            .Replace("global::", string.Empty)
            .Replace("?", string.Empty)
            .Trim();

        var genericStart = cleaned.IndexOf('<');

        if (genericStart >= 0)
            cleaned = cleaned[..genericStart];

        var lastDot = cleaned.LastIndexOf('.');

        return lastDot >= 0
            ? cleaned[(lastDot + 1)..]
            : cleaned;
    }

    private static void AddPathParameters(
        List<string> parameters,
        string groupName,
        ApiEndpointDefinition endpoint,
        KiotaClientMetadata kiotaMetadata)
    {
        foreach (var parameter in endpoint.Parameters
            .Where(x => x.Location.Equals("path", StringComparison.OrdinalIgnoreCase)))
        {
            parameters.Add($"{ResolvePathParameterType(groupName, endpoint, parameter, kiotaMetadata)} {ToCamelCase(NormalizeIdentifier(parameter.Name))}");
        }
    }

    private static void AddQueryParameters(
        List<string> parameters,
        ApiEndpointDefinition endpoint)
    {
        foreach (var parameter in endpoint.Parameters
            .Where(x => x.Location.Equals("query", StringComparison.OrdinalIgnoreCase)))
        {
            parameters.Add($"{ResolveParameterType(parameter, isQueryParameter: true)} {ToCamelCase(NormalizeIdentifier(parameter.Name))}");
        }
    }

    private static void AddHeaderParameters(
        List<string> parameters,
        ApiEndpointDefinition endpoint)
    {
        foreach (var parameter in endpoint.Parameters
            .Where(x => x.Location.Equals("header", StringComparison.OrdinalIgnoreCase)))
        {
            parameters.Add($"string? {ToCamelCase(NormalizeIdentifier(parameter.Name))} = null");
        }
    }

    private static string ResolveParameterType(ApiParameterDefinition parameter, bool isQueryParameter)
    {
        var schemaType = parameter.SchemaType?.Trim().ToLowerInvariant() ?? string.Empty;
        var schemaFormat = parameter.SchemaFormat?.Trim().ToLowerInvariant() ?? string.Empty;
        var name = parameter.Name?.Trim().ToLowerInvariant() ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(schemaType))
        {
            return schemaType switch
            {
                "integer" when schemaFormat == "int64" => "long?",
                "integer" => "int?",
                "number" when schemaFormat == "float" => "float?",
                "number" when schemaFormat == "double" => "double?",
                "number" => "decimal?",
                "boolean" => "bool?",
                "string" when schemaFormat is "date" or "date-time" => "DateTime?",
                "string" => "string",
                _ => parameter.Required && !isQueryParameter ? "string" : "string?"
            };
        }

        if (name is "page" or "pageindex" or "pagenumber" or "pagesize" or "limit" or "offset" or "take" or "skip" or "size" or "count" or "year" or "month" or "day" ||
            name.EndsWith("year", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith("month", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith("day", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith("count", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith("size", StringComparison.OrdinalIgnoreCase))
            return "int?";

        if (name.EndsWith("id", StringComparison.OrdinalIgnoreCase))
            return "string";

        if (name.Contains("date", StringComparison.OrdinalIgnoreCase))
            return "DateTime?";

        return "string?";
    }

    private static string BuildRequestBodyCreationBlock(
        KiotaOperationMetadata? operation)
    {
        return string.Empty;
    }

    private static string BuildTaskReturnType(string returnType)
    {
        return string.IsNullOrWhiteSpace(returnType)
            ? "Task"
            : $"Task<{returnType}>";
    }

    private static string BuildFacadeMethodBody(
        string returnType,
        KiotaOperationMetadata? operation,
        string kiotaCallExpression,
        string kiotaRequestBodyType)
    {
        var requiresObsoleteIndexerSuppression = kiotaCallExpression.Contains('[', StringComparison.Ordinal);
        var requestMappingBlock = BuildRequestMappingBlock(kiotaRequestBodyType);

        if (string.IsNullOrWhiteSpace(returnType))
        {
            var awaitLine = $"        await {kiotaCallExpression}.ConfigureAwait(false);";
            return string.Concat(
                requestMappingBlock,
                WrapWithObsoleteIndexerSuppression(awaitLine, requiresObsoleteIndexerSuppression),
                Environment.NewLine);
        }

        var returnStatement = BuildFacadeReturnStatement(operation, returnType);
        var resultAssignment = $"        var result = await {kiotaCallExpression}.ConfigureAwait(false);";

        resultAssignment = WrapWithObsoleteIndexerSuppression(resultAssignment, requiresObsoleteIndexerSuppression);

        return
            $"{requestMappingBlock}{resultAssignment}{Environment.NewLine}{Environment.NewLine}        {returnStatement}{Environment.NewLine}";
    }

    private static string BuildFacadeReturnStatement(
        KiotaOperationMetadata? operation,
        string returnType)
    {
        if (operation is null)
            return BuildDtoMappingReturnStatement(returnType, "result");

        if (operation.IsCollectionWrapper)
        {
            if (string.IsNullOrWhiteSpace(operation.CollectionPropertyName))
                throw new InvalidOperationException($"A collection wrapper return for '{operation.OperationId}' is missing its collection property metadata.");

            return BuildDtoMappingReturnStatement(returnType, $"result?.{operation.CollectionPropertyName}");
        }

        return BuildDtoMappingReturnStatement(returnType, "result");
    }

    private static string BuildRequestMappingBlock(string kiotaRequestBodyType)
    {
        if (string.IsNullOrWhiteSpace(kiotaRequestBodyType))
            return string.Empty;

        return $"        var kiotaRequest = GeneratedDtoMapper.MapRequired<{NormalizeGeneratedTypeName(kiotaRequestBodyType)}>(request);{Environment.NewLine}{Environment.NewLine}";
    }

    private static string BuildDtoMappingReturnStatement(string contractReturnType, string sourceExpression)
    {
        if (string.IsNullOrWhiteSpace(contractReturnType))
            return "return;";

        if (IsCollectionContractType(contractReturnType))
        {
            var itemType = ExtractInnerTypeFromCollection(contractReturnType);
            return $"return GeneratedDtoMapper.MapList<{itemType}>({sourceExpression});";
        }

        return $"return GeneratedDtoMapper.Map<{contractReturnType.TrimEnd('?')}>({sourceExpression});";
    }

    private static bool IsIgnoredRequestBodyProperty(string propertyName)
    {
        return propertyName.Equals("AdditionalData", StringComparison.OrdinalIgnoreCase)
            || propertyName.Equals("BackingStore", StringComparison.OrdinalIgnoreCase)
            || propertyName.Equals("OdataType", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolvePathParameterType(
        string groupName,
        ApiEndpointDefinition endpoint,
        ApiParameterDefinition parameter,
        KiotaClientMetadata kiotaMetadata)
    {
        var operation = ResolveKiotaOperation(
            groupName,
            endpoint,
            kiotaMetadata,
            allowCrossMethodPathFallback: true);
        var groupMetadata = ResolveKiotaGroupMetadata(groupName, endpoint, kiotaMetadata);

        if (operation is null)
            return ResolvePathParameterTypeFromFallback(groupMetadata, parameter);

        var pathParameter = operation.PathParameters.FirstOrDefault(x =>
            x.Name.Equals(parameter.Name, StringComparison.OrdinalIgnoreCase));

        if (pathParameter is null || string.IsNullOrWhiteSpace(pathParameter.TypeName))
            return ResolvePathParameterTypeFromFallback(groupMetadata, parameter);

        if (IsKiotaInfrastructureType(pathParameter.TypeName))
            return ResolvePathParameterTypeFromFallback(groupMetadata, parameter);

        return NormalizeGeneratedTypeName(pathParameter.TypeName);
    }

    private static string ResolvePathParameterTypeFromFallback(
        KiotaGroupMetadata? groupMetadata,
        ApiParameterDefinition parameter)
    {
        if (groupMetadata is not null &&
            !string.IsNullOrWhiteSpace(groupMetadata.DefaultPathParameterTypeName) &&
            !IsKiotaInfrastructureType(groupMetadata.DefaultPathParameterTypeName))
        {
            return NormalizeGeneratedTypeName(groupMetadata.DefaultPathParameterTypeName);
        }

        return ResolveParameterType(parameter, isQueryParameter: false);
    }

    private static string BuildFacadeArgumentsFromService(
        string groupName,
        ApiEndpointDefinition endpoint,
        KiotaClientMetadata kiotaMetadata,
        AuthenticationMode authenticationMode)
    {
        var arguments = new List<string>();

        AddPathArguments(arguments, endpoint);

        if (endpoint.HasRequestBody)
            arguments.Add("request");

        AddQueryArguments(arguments, endpoint);
        AddHeaderArguments(arguments, endpoint);

        if (endpoint.RequiresAuthorization && authenticationMode == AuthenticationMode.PerMethodToken)
            arguments.Add("accessToken");

        arguments.Add("cancellationToken");

        return string.Join(", ", arguments);
    }

    private static void AddPathArguments(
        List<string> arguments,
        ApiEndpointDefinition endpoint)
    {
        foreach (var parameter in endpoint.Parameters
            .Where(x => x.Location.Equals("path", StringComparison.OrdinalIgnoreCase)))
        {
            arguments.Add(ToCamelCase(NormalizeIdentifier(parameter.Name)));
        }
    }

    private static void AddQueryArguments(
        List<string> arguments,
        ApiEndpointDefinition endpoint)
    {
        foreach (var parameter in endpoint.Parameters
            .Where(x => x.Location.Equals("query", StringComparison.OrdinalIgnoreCase)))
        {
            arguments.Add(ToCamelCase(NormalizeIdentifier(parameter.Name)));
        }
    }

    private static void AddHeaderArguments(
        List<string> arguments,
        ApiEndpointDefinition endpoint)
    {
        foreach (var parameter in endpoint.Parameters
            .Where(x => x.Location.Equals("header", StringComparison.OrdinalIgnoreCase)))
        {
            arguments.Add(ToCamelCase(NormalizeIdentifier(parameter.Name)));
        }
    }

    private static string BuildKiotaCallExpression(
        string apiGroupName,
        ApiEndpointDefinition endpoint,
        KiotaClientMetadata kiotaMetadata,
        string bodyArgumentName,
        AuthenticationMode authenticationMode)
    {
        var groupMetadata = ResolveKiotaGroupMetadata(
            apiGroupName,
            endpoint,
            kiotaMetadata);

        var operation = ResolveKiotaOperation(
            apiGroupName,
            endpoint,
            kiotaMetadata,
            allowCrossMethodPathFallback: true);

        var groupApiProperty = groupMetadata is null
            ? $"_apiClient.{NormalizeIdentifier(apiGroupName)}"
            : $"_apiClient.{groupMetadata.BuilderAccessExpression}";

        var builderChain = BuildKiotaBuilderChain(
            groupApiProperty,
            apiGroupName,
            endpoint,
            operation,
            kiotaMetadata);

        var asyncMethodName = GetKiotaAsyncMethodName(endpoint.Method);
        var configBlock = BuildKiotaRequestConfiguration(endpoint, authenticationMode);
        var bodyArgument = endpoint.HasRequestBody ? $"{bodyArgumentName}, " : string.Empty;

        if (string.IsNullOrWhiteSpace(configBlock))
            return $"{builderChain}.{asyncMethodName}({bodyArgument}cancellationToken: cancellationToken)";

        return $"{builderChain}.{asyncMethodName}({bodyArgument}config =>{Environment.NewLine}        {{{Environment.NewLine}{configBlock}{Environment.NewLine}        }}, cancellationToken: cancellationToken)";
    }

    private static string BuildKiotaBuilderChain(
        string groupApiProperty,
        string apiGroupName,
        ApiEndpointDefinition endpoint,
        KiotaOperationMetadata? operation,
        KiotaClientMetadata kiotaMetadata)
    {
        var groupMetadata = ResolveKiotaGroupMetadata(
            apiGroupName,
            endpoint,
            kiotaMetadata);

        var groupNameForPath = groupMetadata?.GroupName ?? apiGroupName;

        var pathSegmentsAfterGroup = ExtractSegmentsAfterGroupKeepingPathParameters(
            endpoint.Path,
            groupNameForPath);

        if (pathSegmentsAfterGroup.Length == 0)
            return groupApiProperty;

        var chain = groupApiProperty;

        foreach (var segment in pathSegmentsAfterGroup)
        {
            if (IsPathParameterSegment(segment))
            {
                var parameterName = ExtractPathParameterName(segment);
                var parameterAccess = ResolvePathParameterAccessExpression(
                    apiGroupName,
                    endpoint,
                    parameterName,
                    operation,
                    kiotaMetadata);

                chain += parameterAccess;
                continue;
            }

            chain += $".{NormalizeIdentifier(segment)}";
        }

        return chain;
    }

    private static string BuildKiotaRequestConfiguration(
        ApiEndpointDefinition endpoint,
        AuthenticationMode authenticationMode)
    {
        var lines = new List<string>();

        // Só injetamos o Authorization manualmente quando o contrato público ainda expõe o token.
        if (endpoint.RequiresAuthorization && authenticationMode == AuthenticationMode.PerMethodToken)
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
            var assignmentValue = BuildKiotaQueryParameterAssignmentValue(parameterName, query.Name);

            lines.Add($"            config.QueryParameters.{propertyName} = {assignmentValue};");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildKiotaQueryParameterAssignmentValue(
        string parameterName,
        string originalParameterName)
    {
        if (originalParameterName.Contains("date", StringComparison.OrdinalIgnoreCase))
            return $"{parameterName}.HasValue ? new Microsoft.Kiota.Abstractions.Date({parameterName}.Value) : (Microsoft.Kiota.Abstractions.Date?)null";

        return parameterName;
    }

    private static string NormalizeOpenApiPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        return string.Join(
            "/",
            path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(x => !IsVersionSegment(x))
                .Select(x => IsPathParameterSegment(x) ? "{param}" : x.Trim().ToLowerInvariant()));
    }

    private static string ResolveContractReturnType(
        string groupName,
        ApiEndpointDefinition endpoint,
        KiotaClientMetadata kiotaMetadata,
        DtoGenerationContext dtoContext)
    {
        var returnType = ResolveReturnType(groupName, endpoint, kiotaMetadata);

        if (string.IsNullOrWhiteSpace(returnType))
            return string.Empty;

        return MapKiotaTypeToContractType(returnType, dtoContext);
    }

    private static string ResolveContractRequestBodyType(
        string groupName,
        ApiEndpointDefinition endpoint,
        KiotaClientMetadata kiotaMetadata,
        DtoGenerationContext dtoContext)
    {
        var requestBodyType = ResolveRequestBodyType(groupName, endpoint, kiotaMetadata);
        return MapKiotaTypeToContractType(requestBodyType, dtoContext);
    }

    private static string ResolveReturnType(
        string groupName,
        ApiEndpointDefinition endpoint,
        KiotaClientMetadata kiotaMetadata)
    {
        var operation = ResolveKiotaOperation(
            groupName,
            endpoint,
            kiotaMetadata,
            allowCrossMethodPathFallback: false);

        if (operation is null)
        {
            if (endpoint.Method.Equals("DELETE", StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            throw new InvalidOperationException($"Unable to resolve Kiota return type for {endpoint.Method} {endpoint.Path}.");
        }

        if (string.IsNullOrWhiteSpace(operation.ReturnTypeName))
            return string.Empty;

        var normalized = NormalizeGeneratedTypeName(operation.ReturnTypeName);

        if (operation.IsCollection)
            return $"List<{normalized}>?";

        // Make reference types nullable to avoid CS8603 warnings
        if (!normalized.EndsWith("?", StringComparison.Ordinal))
            return $"{normalized}?";

        return normalized;
    }

    private static string ResolveRequestBodyType(
        string groupName,
        ApiEndpointDefinition endpoint,
        KiotaClientMetadata kiotaMetadata)
    {
        var operation = ResolveKiotaOperation(
            groupName,
            endpoint,
            kiotaMetadata,
            allowCrossMethodPathFallback: false);

        if (operation is null || string.IsNullOrWhiteSpace(operation.RequestBodyTypeName))
            throw new InvalidOperationException($"Unable to resolve Kiota request body type for {endpoint.Method} {endpoint.Path}.");

        if (IsKiotaInfrastructureType(operation.RequestBodyTypeName))
            throw new InvalidOperationException($"The resolved Kiota request body type for {endpoint.Method} {endpoint.Path} points to an infrastructure type ('{operation.RequestBodyTypeName}').");

        return NormalizeGeneratedTypeName(operation.RequestBodyTypeName);
    }

    private static KiotaOperationMetadata? ResolveKiotaOperation(
        string apiGroupName,
        ApiEndpointDefinition endpoint,
        KiotaClientMetadata kiotaMetadata,
        bool allowCrossMethodPathFallback)
    {
        // A resolução tenta preservar o vínculo mais forte possível com o Kiota real:
        // verbo + path, depois operationId e por fim shape de navegação.
        var normalizedEndpointPath = NormalizeOpenApiPath(endpoint.Path);
        var groupMetadata = ResolveKiotaGroupMetadata(
            apiGroupName,
            endpoint,
            kiotaMetadata);

        var groupOperations = groupMetadata?.Operations.ToArray() ?? Array.Empty<KiotaOperationMetadata>();
        var allOperations = kiotaMetadata.Groups.SelectMany(g => g.Operations).ToArray();

        var exactGroupPathMatch = groupOperations.FirstOrDefault(x =>
            x.HttpMethod.Equals(endpoint.Method, StringComparison.OrdinalIgnoreCase) &&
            x.EndpointPath.Equals(normalizedEndpointPath, StringComparison.OrdinalIgnoreCase));

        if (exactGroupPathMatch is not null)
            return exactGroupPathMatch;

        var exactPathMatch = allOperations.FirstOrDefault(x =>
            x.HttpMethod.Equals(endpoint.Method, StringComparison.OrdinalIgnoreCase) &&
            x.EndpointPath.Equals(normalizedEndpointPath, StringComparison.OrdinalIgnoreCase));

        if (exactPathMatch is not null)
            return exactPathMatch;

        if (!string.IsNullOrWhiteSpace(endpoint.OperationId))
        {
            var groupOperationIdMatch = groupOperations.FirstOrDefault(x =>
                x.HttpMethod.Equals(endpoint.Method, StringComparison.OrdinalIgnoreCase) &&
                x.OperationId.Equals(endpoint.OperationId, StringComparison.OrdinalIgnoreCase));

            if (groupOperationIdMatch is not null)
                return groupOperationIdMatch;

            var operationIdMatch = allOperations.FirstOrDefault(x =>
                x.HttpMethod.Equals(endpoint.Method, StringComparison.OrdinalIgnoreCase) &&
                x.OperationId.Equals(endpoint.OperationId, StringComparison.OrdinalIgnoreCase));

            if (operationIdMatch is not null)
                return operationIdMatch;
        }

        if (groupMetadata is null)
            return allowCrossMethodPathFallback
                ? allOperations.FirstOrDefault(x =>
                    x.EndpointPath.Equals(normalizedEndpointPath, StringComparison.OrdinalIgnoreCase))
                : null;

        var remainingSegments = ExtractSegmentsAfterGroupKeepingPathParameters(
                endpoint.Path,
                groupMetadata.GroupName)
            .Select(segment => IsPathParameterSegment(segment) ? "Item" : NormalizeIdentifier(segment))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();

        var accessExpression = string.Join(".", remainingSegments);
        var normalizedAccessExpression = NormalizeAccessExpression(accessExpression);

        var accessExpressionMatch = groupOperations.FirstOrDefault(x =>
            x.HttpMethod.Equals(endpoint.Method, StringComparison.OrdinalIgnoreCase) &&
            (string.IsNullOrWhiteSpace(endpoint.OperationId) ||
             x.OperationId.Equals(endpoint.OperationId, StringComparison.OrdinalIgnoreCase)) &&
            (x.AccessExpression.Equals(accessExpression, StringComparison.OrdinalIgnoreCase) ||
             NormalizeAccessExpression(x.AccessExpression)
                 .Equals(normalizedAccessExpression, StringComparison.OrdinalIgnoreCase)));

        if (accessExpressionMatch is not null)
            return accessExpressionMatch;

        if (!allowCrossMethodPathFallback)
            return null;

        var groupPathShapeMatch = groupOperations.FirstOrDefault(x =>
            x.EndpointPath.Equals(normalizedEndpointPath, StringComparison.OrdinalIgnoreCase));

        if (groupPathShapeMatch is not null)
            return groupPathShapeMatch;

        return allOperations.FirstOrDefault(x =>
            x.EndpointPath.Equals(normalizedEndpointPath, StringComparison.OrdinalIgnoreCase) ||
            x.AccessExpression.Equals(accessExpression, StringComparison.OrdinalIgnoreCase) ||
            NormalizeAccessExpression(x.AccessExpression)
                .Equals(normalizedAccessExpression, StringComparison.OrdinalIgnoreCase));
    }

    private static string WrapWithObsoleteIndexerSuppression(string codeLine, bool shouldSuppress)
    {
        if (!shouldSuppress)
            return codeLine;

        return
            $"#pragma warning disable CS0618{Environment.NewLine}{codeLine}{Environment.NewLine}#pragma warning restore CS0618";
    }

    private static string NormalizeAccessExpression(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value
            .Replace(".Item.", ".", StringComparison.OrdinalIgnoreCase)
            .Replace(".Item", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim('.');
    }

    private static KiotaGroupMetadata? ResolveKiotaGroupMetadata(
        string apiGroupName,
        ApiEndpointDefinition endpoint,
        KiotaClientMetadata kiotaMetadata)
    {
        var exactMatch = kiotaMetadata.Groups.FirstOrDefault(x =>
            x.GroupName.Equals(apiGroupName, StringComparison.OrdinalIgnoreCase));

        if (exactMatch is not null)
            return exactMatch;

        var firstBusinessSegment = ExtractFirstBusinessSegment(endpoint.Path);

        if (string.IsNullOrWhiteSpace(firstBusinessSegment))
            return null;

        return kiotaMetadata.Groups.FirstOrDefault(x =>
            x.GroupName.Equals(firstBusinessSegment, StringComparison.OrdinalIgnoreCase));
    }

    private static string ExtractFirstBusinessSegment(string path)
    {
        return path
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !IsVersionSegment(x))
            .Where(x => !x.StartsWith("{") && !x.EndsWith("}"))
            .Select(NormalizeIdentifier)
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
            ?? string.Empty;
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

    private static string[] ExtractSegmentsAfterGroupKeepingPathParameters(
        string path,
        string groupName)
    {
        var segments = path
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !IsVersionSegment(x))
            .ToArray();

        var groupIndex = Array.FindIndex(
            segments,
            x => NormalizeIdentifier(RemoveRouteBraces(x))
                .Equals(groupName, StringComparison.OrdinalIgnoreCase));

        if (groupIndex < 0 || groupIndex + 1 >= segments.Length)
            return Array.Empty<string>();

        return segments
            .Skip(groupIndex + 1)
            .ToArray();
    }

    private static bool IsPathParameterSegment(string segment)
    {
        return !string.IsNullOrWhiteSpace(segment)
            && segment.StartsWith("{", StringComparison.Ordinal)
            && segment.EndsWith("}", StringComparison.Ordinal);
    }

    private static string ExtractPathParameterName(string segment)
    {
        return segment
            .Trim()
            .TrimStart('{')
            .TrimEnd('}');
    }

    private static string RemoveRouteBraces(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
            return string.Empty;

        return segment
            .Trim()
            .TrimStart('{')
            .TrimEnd('}');
    }

    private static string ResolvePathParameterAccessExpression(
        string apiGroupName,
        ApiEndpointDefinition endpoint,
        string pathParameterName,
        KiotaOperationMetadata? operation,
        KiotaClientMetadata kiotaMetadata)
    {
        var parameterName = ToCamelCase(NormalizeIdentifier(pathParameterName));
        var groupMetadata = ResolveKiotaGroupMetadata(apiGroupName, endpoint, kiotaMetadata);

        if (operation is null || operation.PathParameters.Count == 0)
            return ResolvePathAccessFallback(groupMetadata, parameterName);

        var metadata = operation.PathParameters.FirstOrDefault(x =>
            x.Name.Equals(pathParameterName, StringComparison.OrdinalIgnoreCase));

        if (metadata is null || string.IsNullOrWhiteSpace(metadata.AccessExpression))
            return ResolvePathAccessFallback(groupMetadata, parameterName);

        return metadata.AccessExpression;
    }

    private static string ResolvePathAccessFallback(
        KiotaGroupMetadata? groupMetadata,
        string parameterName)
    {
        if (groupMetadata is not null &&
            !string.IsNullOrWhiteSpace(groupMetadata.DefaultPathAccessExpressionTemplate))
        {
            return groupMetadata.DefaultPathAccessExpressionTemplate.Replace(
                "{parameterName}",
                parameterName,
                StringComparison.Ordinal);
        }

        return $"[{parameterName}]";
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
        var pathSegments = path
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(segment => !IsVersionSegment(segment))
            .Select(segment => IsPathParameterSegment(segment) ? "By" + NormalizeIdentifier(ExtractPathParameterName(segment)) : NormalizeIdentifier(segment))
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .ToArray();

        var composedName = string.Concat(pathSegments);

        if (string.IsNullOrWhiteSpace(composedName))
            composedName = string.IsNullOrWhiteSpace(operationId) ? "Operation" : NormalizeIdentifier(operationId);

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

        return string.Concat(parts.Select(part =>
            part.Length == 1
                ? part.ToUpperInvariant()
                : char.ToUpperInvariant(part[0]) + part[1..]));
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

    private static string ToCamelCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        if (value.Length == 1)
            return value.ToLowerInvariant();

        return char.ToLowerInvariant(value[0]) + value[1..];
    }

    private static bool IsCollectionContractType(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return false;

        return typeName.Contains("List<", StringComparison.OrdinalIgnoreCase)
            || typeName.Contains("IList<", StringComparison.OrdinalIgnoreCase)
            || typeName.Contains("ICollection<", StringComparison.OrdinalIgnoreCase)
            || typeName.Contains("IEnumerable<", StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractInnerTypeFromCollection(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return string.Empty;

        var start = typeName.IndexOf('<');

        if (start < 0)
            return typeName;

        var depth = 0;

        for (var i = start; i < typeName.Length; i++)
        {
            if (typeName[i] == '<')
                depth++;

            if (typeName[i] == '>')
                depth--;

            if (depth == 0)
                return typeName[(start + 1)..i].Trim();
        }

        return typeName;
    }

    private static bool IsPrimitiveOrFrameworkType(string typeName)
    {
        var cleaned = TrimNullableSuffix(CleanSourceTypeName(typeName));

        if (string.IsNullOrWhiteSpace(cleaned))
            return true;

        if (cleaned.Equals("string", StringComparison.OrdinalIgnoreCase) ||
            cleaned.Equals("bool", StringComparison.OrdinalIgnoreCase) ||
            cleaned.Equals("byte", StringComparison.OrdinalIgnoreCase) ||
            cleaned.Equals("short", StringComparison.OrdinalIgnoreCase) ||
            cleaned.Equals("int", StringComparison.OrdinalIgnoreCase) ||
            cleaned.Equals("long", StringComparison.OrdinalIgnoreCase) ||
            cleaned.Equals("float", StringComparison.OrdinalIgnoreCase) ||
            cleaned.Equals("double", StringComparison.OrdinalIgnoreCase) ||
            cleaned.Equals("decimal", StringComparison.OrdinalIgnoreCase) ||
            cleaned.Equals("Guid", StringComparison.OrdinalIgnoreCase) ||
            cleaned.Equals("DateTime", StringComparison.OrdinalIgnoreCase) ||
            cleaned.Equals("Date", StringComparison.OrdinalIgnoreCase) ||
            cleaned.Equals("TimeSpan", StringComparison.OrdinalIgnoreCase) ||
            cleaned.Equals("byte[]", StringComparison.OrdinalIgnoreCase) ||
            cleaned.Equals("Binary", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (cleaned.StartsWith("System.", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private static bool IsKiotaInfrastructureType(string typeName)
    {
        var cleaned = TrimNullableSuffix(CleanSourceTypeName(typeName));

        if (string.IsNullOrWhiteSpace(cleaned))
            return true;

        if (cleaned.Equals("IRequestAdapter", StringComparison.OrdinalIgnoreCase) ||
            cleaned.Equals("RequestAdapter", StringComparison.OrdinalIgnoreCase) ||
            cleaned.EndsWith(".IRequestAdapter", StringComparison.OrdinalIgnoreCase) ||
            cleaned.EndsWith(".RequestAdapter", StringComparison.OrdinalIgnoreCase) ||
            cleaned.Equals("PathParameters", StringComparison.OrdinalIgnoreCase) ||
            cleaned.EndsWith(".PathParameters", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return cleaned.Equals("Dictionary<string, object>", StringComparison.OrdinalIgnoreCase)
            || cleaned.Equals("IDictionary<string, object>", StringComparison.OrdinalIgnoreCase)
            || cleaned.Equals("System.Collections.Generic.Dictionary<string, object>", StringComparison.OrdinalIgnoreCase)
            || cleaned.Equals("System.Collections.Generic.IDictionary<string, object>", StringComparison.OrdinalIgnoreCase);
    }

    private static string CleanSourceTypeName(string typeName)
    {
        return typeName
            .Replace("global::", string.Empty)
            .Replace("\r", string.Empty)
            .Replace("\n", string.Empty)
            .Trim();
    }

    private static bool HasNullableSuffix(string typeName)
    {
        return CleanSourceTypeName(typeName).EndsWith("?", StringComparison.Ordinal);
    }

    private static string TrimNullableSuffix(string typeName)
    {
        var cleaned = CleanSourceTypeName(typeName);
        return cleaned.EndsWith("?", StringComparison.Ordinal)
            ? cleaned[..^1]
            : cleaned;
    }

    private static string ExtractNamespace(string typeName)
    {
        var cleaned = TrimNullableSuffix(typeName);
        var genericStart = cleaned.IndexOf('<');

        if (genericStart >= 0)
            cleaned = cleaned[..genericStart];

        var lastDot = cleaned.LastIndexOf('.');
        return lastDot > 0
            ? cleaned[..lastDot]
            : string.Empty;
    }

    private static bool TryGetGenericTypeDefinition(
        string typeName,
        out string genericTypeName,
        out string[] genericArguments)
    {
        var cleaned = TrimNullableSuffix(typeName);
        var genericStart = cleaned.IndexOf('<');

        if (genericStart < 0 || !cleaned.EndsWith('>'))
        {
            genericTypeName = string.Empty;
            genericArguments = Array.Empty<string>();
            return false;
        }

        genericTypeName = cleaned[..genericStart].Trim();
        var argumentsText = cleaned[(genericStart + 1)..^1];
        genericArguments = SplitGenericArguments(argumentsText);
        return genericArguments.Length > 0;
    }

    private static string[] SplitGenericArguments(string argumentsText)
    {
        if (string.IsNullOrWhiteSpace(argumentsText))
            return Array.Empty<string>();

        var arguments = new List<string>();
        var current = new List<char>();
        var depth = 0;

        foreach (var character in argumentsText)
        {
            if (character == '<')
                depth++;

            if (character == '>')
                depth--;

            if (character == ',' && depth == 0)
            {
                arguments.Add(new string(current.ToArray()).Trim());
                current.Clear();
                continue;
            }

            current.Add(character);
        }

        if (current.Count > 0)
            arguments.Add(new string(current.ToArray()).Trim());

        return arguments
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();
    }

    private static string NormalizeGeneratedTypeName(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            throw new InvalidOperationException("A generated Kiota type name was expected, but none was found.");

        return typeName.Trim() switch
        {
            "Date" => "DateTime",
            "Date?" => "DateTime?",
            "MultipartBody" => "Microsoft.Kiota.Abstractions.MultipartBody",
            "MultipartBody?" => "Microsoft.Kiota.Abstractions.MultipartBody?",
            _ => typeName
        };
    }

    private static string BuildFullyQualifiedTypeName(string rootNamespace, string typeName)
    {
        if (string.IsNullOrWhiteSpace(rootNamespace))
            return typeName;

        if (string.IsNullOrWhiteSpace(typeName))
            return rootNamespace;

        return $"global::{rootNamespace}.{typeName}";
    }

    private static string ResolveEffectiveClientClassName(
        string apiClientProjectPath,
        string targetRootNamespace,
        string preferredClientClassName,
        string fallbackClientClassName)
    {
        if (!string.IsNullOrWhiteSpace(preferredClientClassName) &&
            File.Exists(Path.Combine(apiClientProjectPath, $"{preferredClientClassName}.cs")))
        {
            return preferredClientClassName;
        }

        if (!string.IsNullOrWhiteSpace(fallbackClientClassName) &&
            File.Exists(Path.Combine(apiClientProjectPath, $"{fallbackClientClassName}.cs")))
        {
            return fallbackClientClassName;
        }

        foreach (var filePath in Directory.GetFiles(apiClientProjectPath, "*.cs", SearchOption.AllDirectories))
        {
            var content = File.ReadAllText(filePath);

            if (!string.IsNullOrWhiteSpace(targetRootNamespace) &&
                !content.Contains($"namespace {targetRootNamespace};", StringComparison.Ordinal))
            {
                continue;
            }

            var match = Regex.Match(
                content,
                @"public\s+partial\s+class\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)",
                RegexOptions.CultureInvariant);

            if (match.Success)
            {
                var candidate = match.Groups["name"].Value.Trim();

                if (!string.IsNullOrWhiteSpace(candidate) &&
                    candidate.EndsWith("Client", StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }
        }

        return !string.IsNullOrWhiteSpace(preferredClientClassName)
            ? preferredClientClassName
            : fallbackClientClassName;
    }

    private static void RenameKiotaClientClass(
        string apiClientProjectPath,
        string originalClientClassName,
        string targetClientClassName,
        string targetRootNamespace)
    {
        if (string.IsNullOrWhiteSpace(apiClientProjectPath) ||
            !Directory.Exists(apiClientProjectPath) ||
            string.IsNullOrWhiteSpace(originalClientClassName) ||
            string.IsNullOrWhiteSpace(targetClientClassName) ||
            originalClientClassName.Equals(targetClientClassName, StringComparison.Ordinal))
        {
            return;
        }

        var oldFile = Directory
            .GetFiles(apiClientProjectPath, $"{originalClientClassName}.cs", SearchOption.AllDirectories)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(oldFile))
        {
            oldFile = Directory
                .GetFiles(apiClientProjectPath, "*.cs", SearchOption.AllDirectories)
                .FirstOrDefault(file =>
                {
                    var content = File.ReadAllText(file);
                    return content.Contains($"public partial class {originalClientClassName}", StringComparison.Ordinal);
                });
        }

        if (string.IsNullOrWhiteSpace(oldFile))
            return;

        var oldClassName = originalClientClassName;
        var newClassName = targetClientClassName;
        var newFile = Path.Combine(Path.GetDirectoryName(oldFile)!, $"{newClassName}.cs");

        var content = File.ReadAllText(oldFile);

        content = content.Replace(
            $"public partial class {oldClassName}",
            $"public partial class {newClassName}");

        content = content.Replace(
            $"public {oldClassName}(",
            $"public {newClassName}(");

        if (!string.IsNullOrWhiteSpace(targetRootNamespace))
        {
            content = Regex.Replace(
                content,
                @"namespace\s+[A-Za-z0-9_.]+(?=\s*[;{])",
                $"namespace {targetRootNamespace}",
                RegexOptions.CultureInvariant);
        }

        File.WriteAllText(newFile, content);
        File.Delete(oldFile);
    }

    private static void RewriteKiotaClientNamespaces(
        string apiClientProjectPath,
        ModernizationRequest request,
        KiotaClientMetadata kiotaMetadata,
        string targetRootNamespace)
    {
        if (string.IsNullOrWhiteSpace(apiClientProjectPath) ||
            !Directory.Exists(apiClientProjectPath) ||
            string.IsNullOrWhiteSpace(targetRootNamespace))
        {
            return;
        }

        var originalRootNamespaces = BuildOriginalApiClientNamespaceCandidates(request, kiotaMetadata)
            .Select(candidate => ExtractApiClientNamespaceFamilyRoot(candidate) ?? candidate)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Where(x => !x.Equals(targetRootNamespace, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(x => x.Length)
            .ToArray();

        if (originalRootNamespaces.Length == 0)
            return;

        foreach (var filePath in Directory.GetFiles(apiClientProjectPath, "*.cs", SearchOption.AllDirectories))
        {
            var content = File.ReadAllText(filePath);

            if (string.IsNullOrWhiteSpace(content))
            {
                continue;
            }

            var rewrittenContent = content;

            foreach (var originalRootNamespace in originalRootNamespaces)
            {
                if (!rewrittenContent.Contains(originalRootNamespace, StringComparison.Ordinal))
                    continue;

                rewrittenContent = rewrittenContent.Replace(
                    $"global::{originalRootNamespace}.",
                    $"global::{targetRootNamespace}.",
                    StringComparison.Ordinal);

                rewrittenContent = rewrittenContent.Replace(
                    $"global::{originalRootNamespace}",
                    $"global::{targetRootNamespace}",
                    StringComparison.Ordinal);

                rewrittenContent = rewrittenContent.Replace(
                    $"using {originalRootNamespace}.",
                    $"using {targetRootNamespace}.",
                    StringComparison.Ordinal);

                rewrittenContent = rewrittenContent.Replace(
                    $"using {originalRootNamespace};",
                    $"using {targetRootNamespace};",
                    StringComparison.Ordinal);

                rewrittenContent = rewrittenContent.Replace(
                    $"namespace {originalRootNamespace}.",
                    $"namespace {targetRootNamespace}.",
                    StringComparison.Ordinal);

                rewrittenContent = Regex.Replace(
                    rewrittenContent,
                    $@"namespace\s+{Regex.Escape(originalRootNamespace)}(?=\s*[;{{])",
                    $"namespace {targetRootNamespace}",
                    RegexOptions.CultureInvariant);

                rewrittenContent = rewrittenContent.Replace(
                    $"<{originalRootNamespace}.",
                    $"<{targetRootNamespace}.",
                    StringComparison.Ordinal);

                rewrittenContent = rewrittenContent.Replace(
                    $"({originalRootNamespace}.",
                    $"({targetRootNamespace}.",
                    StringComparison.Ordinal);
            }

            if (!rewrittenContent.Equals(content, StringComparison.Ordinal))
                File.WriteAllText(filePath, rewrittenContent);
        }
    }

    private static IReadOnlyCollection<string> BuildOriginalApiClientNamespaceCandidates(
        ModernizationRequest request,
        KiotaClientMetadata kiotaMetadata)
    {
        var candidates = new HashSet<string>(StringComparer.Ordinal)
        {
            request.ProjectName + ".ApiClient",
            request.BaseNamespace + ".ApiClient"
        };

        if (request.EmbeddedProjectPrefix is not null)
            candidates.Add($"{request.EmbeddedProjectPrefix}.ApiClient");

        if (!string.IsNullOrWhiteSpace(kiotaMetadata.RootNamespace))
        {
            candidates.Add(kiotaMetadata.RootNamespace);

            var familyRoot = ExtractApiClientNamespaceFamilyRoot(kiotaMetadata.RootNamespace);

            if (!string.IsNullOrWhiteSpace(familyRoot))
                candidates.Add(familyRoot);
        }

        return candidates;
    }

    private static string ExtractApiClientNamespaceFamilyRoot(string namespaceName)
    {
        if (string.IsNullOrWhiteSpace(namespaceName))
            return string.Empty;

        var marker = ".ApiClient";
        var index = namespaceName.IndexOf(marker, StringComparison.Ordinal);

        if (index < 0)
            return string.Empty;

        return namespaceName[..(index + marker.Length)];
    }
}
