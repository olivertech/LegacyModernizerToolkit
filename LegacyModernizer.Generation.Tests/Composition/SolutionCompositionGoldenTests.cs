using LegacyModernizer.Application.DTOs.Common;
using LegacyModernizer.Application.DTOs.Commons;
using LegacyModernizer.Domain.Entities;
using LegacyModernizer.Domain.Enums;
using LegacyModernizer.Domain.ValueObjects;
using LegacyModernizer.Generation.Composition;
using LegacyModernizer.Generation.Tests.Scaffolding;

namespace LegacyModernizer.Generation.Tests.Composition;

public sealed class SolutionCompositionGoldenTests : IDisposable
{
    private readonly string _tempRootPath;

    public SolutionCompositionGoldenTests()
    {
        _tempRootPath = Path.Combine(Path.GetTempPath(), "LegacyModernizerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRootPath);
    }

    [Fact]
    public async Task ComposeAsync_GeneratesExpectedGoldenFilesAndManifest()
    {
        var generatedClientPath = Path.Combine(_tempRootPath, "generated-client");
        Directory.CreateDirectory(generatedClientPath);

        WriteFakeClient(generatedClientPath);

        var workspace = CreateWorkspace();
        var request = CreateRequest();
        var artifact = new GeneratedArtifact(
            ArtifactType.GeneratedClient,
            new ArtifactLocation(generatedClientPath),
            ExecutionStep.ClientGeneration);

        var groups = new[]
        {
            new ApiGroupDefinition
            {
                Name = "Authentication",
                Endpoints =
                [
                    new ApiEndpointDefinition
                    {
                        Path = "/v1/authentication/login",
                        Method = "POST",
                        OperationId = "Login",
                        HasRequestBody = true
                    },
                    new ApiEndpointDefinition
                    {
                        Path = "/v1/authentication/me",
                        Method = "GET",
                        OperationId = "GetMe"
                    }
                ]
            }
        };

        var kiotaMetadata = new KiotaClientMetadata
        {
            RootNamespace = "Fake.Client",
            ClientClassName = "ApiClient",
            Groups =
            [
                new KiotaGroupMetadata
                {
                    GroupName = "Authentication",
                    BuilderAccessExpression = "Authentication",
                    Operations =
                    [
                        new KiotaOperationMetadata
                        {
                            OperationId = "Login",
                            MethodName = "PostAsync",
                            HttpMethod = "POST",
                            EndpointPath = "authentication/login",
                            AccessExpression = "Login",
                            RequestBodyTypeName = "Fake.Client.Authentication.Login.LoginPostRequestBody",
                            ReturnTypeName = "Fake.Client.Models.AuthResponse?"
                        },
                        new KiotaOperationMetadata
                        {
                            OperationId = "GetMe",
                            MethodName = "GetAsync",
                            HttpMethod = "GET",
                            EndpointPath = "authentication/me",
                            AccessExpression = "Me",
                            ReturnTypeName = "Fake.Client.Models.UserProfileResponse?"
                        }
                    ]
                }
            ]
        };

        var service = new SolutionCompositionService();
        var solution = await service.ComposeAsync(request, workspace, artifact, groups, kiotaMetadata);

        var repoRoot = GoldenFileAssert.RepositoryRoot();
        var goldenRoot = Path.Combine(repoRoot, "LegacyModernizer.Generation.Tests", "GoldenFiles", "Composition", "Minimal");

        GoldenFileAssert.Matches(
            Path.Combine(solution.RootPath, "src", "GoldenSample.Core", "Interfaces", "IApiFacade.cs"),
            Path.Combine(goldenRoot, "IApiFacade.snap"));

        GoldenFileAssert.Matches(
            Path.Combine(solution.RootPath, "src", "GoldenSample.Core", "Dtos", "LoginPostRequestBodyDto.cs"),
            Path.Combine(goldenRoot, "LoginPostRequestBodyDto.snap"));

        GoldenFileAssert.Matches(
            Path.Combine(solution.RootPath, "src", "GoldenSample.Core", "Dtos", "LoginPostRequestBody_providerDto.cs"),
            Path.Combine(goldenRoot, "LoginPostRequestBody_providerDto.snap"));

        GoldenFileAssert.Matches(
            Path.Combine(solution.RootPath, "src", "GoldenSample.Infrastructure", "Mappers", "GeneratedDtoMapper.cs"),
            Path.Combine(goldenRoot, "GeneratedDtoMapper.snap"));

        GoldenFileAssert.Matches(
            Path.Combine(solution.RootPath, "src", "GoldenSample.Infrastructure", "Facades", "ApiFacade.Authentication.cs"),
            Path.Combine(goldenRoot, "ApiFacade.Authentication.snap"));

        GoldenFileAssert.Matches(
            Path.Combine(solution.RootPath, "generation-manifest.json"),
            Path.Combine(goldenRoot, "generation-manifest.json"));
    }

    [Fact]
    public async Task ComposeAsync_Embedded_GeneratesExpectedIntegrationArtifacts()
    {
        var generatedClientPath = Path.Combine(_tempRootPath, "generated-client-embedded");
        Directory.CreateDirectory(generatedClientPath);

        WriteFakeClient(generatedClientPath);

        var workspace = CreateWorkspace();
        var request = CreateRequest(
            GenerationMode.Embedded,
            AuthenticationMode.AccessTokenAccessor,
            "AlphaSquad");
        var artifact = new GeneratedArtifact(
            ArtifactType.GeneratedClient,
            new ArtifactLocation(generatedClientPath),
            ExecutionStep.ClientGeneration);

        var groups = new[]
        {
            new ApiGroupDefinition
            {
                Name = "Authentication",
                Endpoints =
                [
                    new ApiEndpointDefinition
                    {
                        Path = "/v1/authentication/login",
                        Method = "POST",
                        OperationId = "Login",
                        HasRequestBody = true
                    },
                    new ApiEndpointDefinition
                    {
                        Path = "/v1/authentication/me",
                        Method = "GET",
                        OperationId = "GetMe",
                        RequiresAuthorization = true
                    }
                ]
            }
        };

        var kiotaMetadata = new KiotaClientMetadata
        {
            RootNamespace = "Fake.Client",
            ClientClassName = "ApiClient",
            Groups =
            [
                new KiotaGroupMetadata
                {
                    GroupName = "Authentication",
                    BuilderAccessExpression = "Authentication",
                    Operations =
                    [
                        new KiotaOperationMetadata
                        {
                            OperationId = "Login",
                            MethodName = "PostAsync",
                            HttpMethod = "POST",
                            EndpointPath = "authentication/login",
                            AccessExpression = "Login",
                            RequestBodyTypeName = "Fake.Client.Authentication.Login.LoginPostRequestBody",
                            ReturnTypeName = "Fake.Client.Models.AuthResponse?"
                        },
                        new KiotaOperationMetadata
                        {
                            OperationId = "GetMe",
                            MethodName = "GetAsync",
                            HttpMethod = "GET",
                            EndpointPath = "authentication/me",
                            AccessExpression = "Me",
                            ReturnTypeName = "Fake.Client.Models.UserProfileResponse?"
                        }
                    ]
                }
            ]
        };

        var service = new SolutionCompositionService();
        var solution = await service.ComposeAsync(request, workspace, artifact, groups, kiotaMetadata);

        var repoRoot = GoldenFileAssert.RepositoryRoot();
        var goldenRoot = Path.Combine(repoRoot, "LegacyModernizer.Generation.Tests", "GoldenFiles", "Composition", "Embedded");

        GoldenFileAssert.Matches(
            Path.Combine(solution.RootPath, "src", "AlphaSquad.Lmt.Application.Contracts", "Interfaces", "IApiFacade.cs"),
            Path.Combine(goldenRoot, "IApiFacade.snap"));

        GoldenFileAssert.Matches(
            Path.Combine(solution.RootPath, "src", "AlphaSquad.Lmt.Application.Contracts", "Interfaces", "IAccessTokenAccessor.cs"),
            Path.Combine(goldenRoot, "IAccessTokenAccessor.snap"));

        GoldenFileAssert.Matches(
            Path.Combine(solution.RootPath, "src", "AlphaSquad.Lmt.Application.Http", "DependencyInjection", "ServiceCollectionExtensions.cs"),
            Path.Combine(goldenRoot, "ServiceCollectionExtensions.snap"));

        GoldenFileAssert.Matches(
            Path.Combine(solution.RootPath, "generation-manifest.json"),
            Path.Combine(goldenRoot, "generation-manifest.json"));

        GoldenFileAssert.Matches(
            Path.Combine(solution.RootPath, "integration-manifest.json"),
            Path.Combine(goldenRoot, "integration-manifest.json"));

        GoldenFileAssert.Matches(
            Path.Combine(solution.RootPath, "INTEGRATION.md"),
            Path.Combine(goldenRoot, "INTEGRATION.md"));
    }

    private Workspace CreateWorkspace()
    {
        var rootPath = Path.Combine(_tempRootPath, "workspace");
        var inputPath = Path.Combine(rootPath, "input");
        var generatedPath = Path.Combine(rootPath, "generated");
        var composedPath = Path.Combine(rootPath, "composed");
        var packagePath = Path.Combine(rootPath, "package");

        Directory.CreateDirectory(inputPath);
        Directory.CreateDirectory(generatedPath);
        Directory.CreateDirectory(composedPath);
        Directory.CreateDirectory(packagePath);

        var workspace = new Workspace(new WorkspacePaths(
            rootPath,
            inputPath,
            generatedPath,
            composedPath,
            packagePath));

        workspace.MarkAsPrepared();
        return workspace;
    }

    private ModernizationRequest CreateRequest(
        GenerationMode generationMode = GenerationMode.Standalone,
        AuthenticationMode authenticationMode = AuthenticationMode.PerMethodToken,
        string? embeddedProjectPrefix = null)
    {
        var specificationPath = Path.Combine(_tempRootPath, "openapi.json");
        File.WriteAllText(specificationPath, "{}");

        return new ModernizationRequest(
            new SpecificationSource(SpecificationSourceType.File, specificationPath),
            new ProjectName("GoldenSample"),
            new NamespaceName("Golden.Sample"),
            "net8.0",
            generationMode,
            authenticationMode,
            string.IsNullOrWhiteSpace(embeddedProjectPrefix)
                ? null
                : new EmbeddedProjectPrefix(embeddedProjectPrefix));
    }

    private static void WriteFakeClient(string generatedClientPath)
    {
        WriteFile(
            generatedClientPath,
            "ApiClient.cs",
            """
            namespace Fake.Client;

            public partial class ApiClient
            {
            }
            """);

        WriteFile(
            generatedClientPath,
            Path.Combine("Authentication", "Login", "LoginPostRequestBody.cs"),
            """
            namespace Fake.Client.Authentication.Login;

            public class LoginPostRequestBody
            {
                public string? Email { get; set; }
                public global::Fake.Client.Authentication.Login.LoginPostRequestBody_provider? Provider { get; set; }
            }
            """);

        WriteFile(
            generatedClientPath,
            Path.Combine("Authentication", "Login", "LoginPostRequestBody_provider.cs"),
            """
            namespace Fake.Client.Authentication.Login;

            public enum LoginPostRequestBody_provider
            {
                [global::System.Runtime.Serialization.EnumMember(Value = "google")]
                Google,
                [global::System.Runtime.Serialization.EnumMember(Value = "apple")]
                Apple,
            }
            """);

        WriteFile(
            generatedClientPath,
            Path.Combine("Models", "AuthResponse.cs"),
            """
            namespace Fake.Client.Models;

            public class AuthResponse
            {
                public string? AccessToken { get; set; }
            }
            """);

        WriteFile(
            generatedClientPath,
            Path.Combine("Models", "UserProfileResponse.cs"),
            """
            namespace Fake.Client.Models;

            public class UserProfileResponse
            {
                public string? DisplayName { get; set; }
            }
            """);
    }

    private static void WriteFile(string rootPath, string relativePath, string content)
    {
        var fullPath = Path.Combine(rootPath, relativePath);
        var directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(fullPath, content.Replace("\r\n", "\n").Replace("\n", Environment.NewLine));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRootPath))
            Directory.Delete(_tempRootPath, recursive: true);
    }
}
