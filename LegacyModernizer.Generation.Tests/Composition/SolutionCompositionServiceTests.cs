using System.Reflection;
using System.Text.Json;
using LegacyModernizer.Application.DTOs.Common;
using LegacyModernizer.Application.DTOs.Commons;
using LegacyModernizer.Domain.Entities;
using LegacyModernizer.Domain.Enums;
using LegacyModernizer.Domain.ValueObjects;
using LegacyModernizer.Generation.Composition;

namespace LegacyModernizer.Generation.Tests.Composition;

public sealed class SolutionCompositionServiceTests
{
    private static object ResolveProjectLayout(ModernizationRequest request)
    {
        var resolverType = typeof(SolutionCompositionService).Assembly.GetType(
            "LegacyModernizer.Generation.Composition.ProjectNamingStrategyResolver");

        Assert.NotNull(resolverType);

        var resolveMethod = resolverType!.GetMethod(
            "Resolve",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.NotNull(resolveMethod);

        var layout = resolveMethod!.Invoke(null, [request]);
        Assert.NotNull(layout);
        return layout!;
    }

    private static string GetLayoutValue(object layout, string propertyName)
    {
        var property = layout.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.NotNull(property);

        return Assert.IsType<string>(property!.GetValue(layout));
    }

    private static object CreateDtoContext()
    {
        var nestedType = typeof(SolutionCompositionService).GetNestedType(
            "DtoGenerationContext",
            BindingFlags.NonPublic);

        Assert.NotNull(nestedType);

        var instance = Activator.CreateInstance(nestedType!, nonPublic: true);
        Assert.NotNull(instance);

        nestedType!.GetProperty("BaseNamespace", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .SetValue(instance, "Fake.Project");

        nestedType.GetProperty("ClientRootPath", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .SetValue(instance, Path.GetTempPath());

        return instance!;
    }

    private static void AddDtoMapping(object dtoContext, string sourceTypeName, string dtoTypeName)
    {
        var sourceToDtoTypeName = dtoContext.GetType()
            .GetProperty("SourceToDtoTypeName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .GetValue(dtoContext);

        var addMethod = sourceToDtoTypeName!.GetType().GetMethod("Add", [typeof(string), typeof(string)]);
        Assert.NotNull(addMethod);
        addMethod!.Invoke(sourceToDtoTypeName, [sourceTypeName, dtoTypeName]);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "LegacyModernizerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    [Fact]
    public void ProjectNamingStrategyResolver_UsesEmbeddedNamingConvention()
    {
        var request = new ModernizationRequest(
            new SpecificationSource(SpecificationSourceType.File, Path.Combine(Path.GetTempPath(), "openapi.json")),
            new ProjectName("AlphaSquad"),
            new NamespaceName("AlphaSquad"),
            "net10.0",
            GenerationMode.Embedded,
            AuthenticationMode.AccessTokenAccessor,
            new EmbeddedProjectPrefix("AlphaSquad"));

        var layout = ResolveProjectLayout(request);

        Assert.Equal("AlphaSquad.Lmt.Application.Contracts", GetLayoutValue(layout, "ContractsProjectName"));
        Assert.Equal("AlphaSquad.Lmt.Application.ApiClient", GetLayoutValue(layout, "ApiClientProjectName"));
        Assert.Equal("AlphaSquad.Lmt.Application.Http", GetLayoutValue(layout, "HttpProjectName"));
        Assert.Equal("AlphaSquad.Lmt.Application.Contracts", GetLayoutValue(layout, "ContractsNamespace"));
        Assert.Equal("AlphaSquad.Lmt.Application.ApiClient", GetLayoutValue(layout, "ApiClientNamespace"));
        Assert.Equal("AlphaSquad.Lmt.Application.Http", GetLayoutValue(layout, "HttpNamespace"));
    }

    [Fact]
    public void BuildFacadeMethodParameters_UsesTypedIndexerPathParameterType()
    {
        var endpoint = new ApiEndpointDefinition
        {
            Path = "/v1/notifications/{id}",
            Method = "GET",
            Parameters =
            [
                new ApiParameterDefinition
                {
                    Name = "id",
                    Location = "path",
                    Required = true
                }
            ]
        };

        var metadata = new KiotaClientMetadata
        {
            Groups =
            [
                new KiotaGroupMetadata
                {
                    GroupName = "Notifications",
                    BuilderAccessExpression = "Notifications",
                    DefaultPathParameterTypeName = "int?",
                    DefaultPathAccessExpressionTemplate = "[{parameterName}]",
                    Operations =
                    [
                        new KiotaOperationMetadata
                        {
                            HttpMethod = "GET",
                            EndpointPath = "notifications/{param}",
                            ReturnTypeName = "Fake.Client.Models.NotificationResponse?",
                            AccessExpression = "Item",
                            PathParameters =
                            [
                                new KiotaPathParameterMetadata
                                {
                                    Name = "id",
                                    AccessExpression = "[id]",
                                    TypeName = "int?"
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        var method = typeof(SolutionCompositionService).GetMethod(
            "BuildFacadeMethodParameters",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(method);

        var dtoContext = CreateDtoContext();

        var parameters = (string?)method!.Invoke(
            null,
            [ "Notifications", endpoint, metadata, dtoContext, AuthenticationMode.PerMethodToken ]);

        Assert.Equal("int? id, CancellationToken cancellationToken = default", parameters);
    }

    [Fact]
    public void BuildFacadeMethodParameters_FallsBackToOpenApiSchemaTypeForPathParameters()
    {
        var endpoint = new ApiEndpointDefinition
        {
            Path = "/v1/notifications/{id}",
            Method = "GET",
            Parameters =
            [
                new ApiParameterDefinition
                {
                    Name = "id",
                    Location = "path",
                    Required = true,
                    SchemaType = "integer",
                    SchemaFormat = "int32"
                }
            ]
        };

        var metadata = new KiotaClientMetadata
        {
            Groups =
            [
                new KiotaGroupMetadata
                {
                    GroupName = "Notifications",
                    BuilderAccessExpression = "Notifications"
                }
            ]
        };

        var method = typeof(SolutionCompositionService).GetMethod(
            "BuildFacadeMethodParameters",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(method);

        var dtoContext = CreateDtoContext();

        var parameters = (string?)method!.Invoke(
            null,
            [ "Notifications", endpoint, metadata, dtoContext, AuthenticationMode.PerMethodToken ]);

        Assert.Equal("int? id, CancellationToken cancellationToken = default", parameters);
    }

    [Fact]
    public void BuildFacadeMethodParameters_FallsBackWhenKiotaPathParameterTypeLeaksInfrastructureTypes()
    {
        var endpoint = new ApiEndpointDefinition
        {
            Path = "/v1/tenants/{slug}",
            Method = "GET",
            Parameters =
            [
                new ApiParameterDefinition
                {
                    Name = "slug",
                    Location = "path",
                    Required = true,
                    SchemaType = "string"
                }
            ]
        };

        var metadata = new KiotaClientMetadata
        {
            Groups =
            [
                new KiotaGroupMetadata
                {
                    GroupName = "Tenants",
                    BuilderAccessExpression = "Tenants",
                    DefaultPathParameterTypeName = "System.Collections.Generic.Dictionary<string, object>",
                    Operations =
                    [
                        new KiotaOperationMetadata
                        {
                            HttpMethod = "GET",
                            EndpointPath = "tenants/{param}",
                            ReturnTypeName = "Fake.Client.Models.TenantResponse?",
                            PathParameters =
                            [
                                new KiotaPathParameterMetadata
                                {
                                    Name = "slug",
                                    AccessExpression = ".BySlug(slug)",
                                    TypeName = "Microsoft.Kiota.Abstractions.IRequestAdapter"
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        var method = typeof(SolutionCompositionService).GetMethod(
            "BuildFacadeMethodParameters",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(method);

        var dtoContext = CreateDtoContext();

        var parameters = (string?)method!.Invoke(
            null,
            [ "Tenants", endpoint, metadata, dtoContext, AuthenticationMode.AccessTokenAccessor ]);

        Assert.Equal("string slug, CancellationToken cancellationToken = default", parameters);
    }

    [Fact]
    public void BuildFacadeMethodBody_WrapsIndexerCallsWithLocalObsoleteSuppression()
    {
        var method = typeof(SolutionCompositionService).GetMethod(
            "BuildFacadeMethodBody",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(method);

        var methodBody = (string?)method!.Invoke(
            null,
            [ "Fake.Client.Models.NotificationResponse?", null, "_apiClient.Notifications[id].GetAsync(cancellationToken: cancellationToken)", string.Empty ]);

        Assert.NotNull(methodBody);
        Assert.Contains("#pragma warning disable CS0618", methodBody, StringComparison.Ordinal);
        Assert.Contains("_apiClient.Notifications[id].GetAsync", methodBody, StringComparison.Ordinal);
        Assert.Contains("#pragma warning restore CS0618", methodBody, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildFacadeMethodParameters_IncludesRequestQueryHeaderAndAuthorizationParameters()
    {
        var endpoint = new ApiEndpointDefinition
        {
            Path = "/v1/reports/{id}",
            Method = "POST",
            HasRequestBody = true,
            RequiresAuthorization = true,
            Parameters =
            [
                new ApiParameterDefinition
                {
                    Name = "id",
                    Location = "path",
                    Required = true,
                    SchemaType = "integer",
                    SchemaFormat = "int32"
                },
                new ApiParameterDefinition
                {
                    Name = "includeArchived",
                    Location = "query",
                    SchemaType = "boolean"
                },
                new ApiParameterDefinition
                {
                    Name = "x-correlation-id",
                    Location = "header"
                }
            ]
        };

        var metadata = new KiotaClientMetadata
        {
            Groups =
            [
                new KiotaGroupMetadata
                {
                    GroupName = "Reports",
                    BuilderAccessExpression = "Reports",
                    Operations =
                    [
                        new KiotaOperationMetadata
                        {
                            HttpMethod = "POST",
                            EndpointPath = "reports/{param}",
                            RequestBodyTypeName = "Fake.Client.Reports.ReportsPostRequestBody"
                        }
                    ]
                }
            ]
        };

        var method = typeof(SolutionCompositionService).GetMethod(
            "BuildFacadeMethodParameters",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(method);

        var dtoContext = CreateDtoContext();
        AddDtoMapping(dtoContext, "Fake.Client.Reports.ReportsPostRequestBody", "ReportsPostRequestBodyDto");

        var parameters = (string?)method!.Invoke(
            null,
            [ "Reports", endpoint, metadata, dtoContext, AuthenticationMode.PerMethodToken ]);

        Assert.Equal(
            "int? id, ReportsPostRequestBodyDto request, bool? includeArchived, string? xCorrelationId = null, string? accessToken = null, CancellationToken cancellationToken = default",
            parameters);
    }

    [Fact]
    public void BuildFacadeMethodParameters_OmitsAccessTokenWhenUsingAccessTokenAccessorMode()
    {
        var endpoint = new ApiEndpointDefinition
        {
            Path = "/v1/reports/{id}",
            Method = "POST",
            HasRequestBody = true,
            RequiresAuthorization = true,
            Parameters =
            [
                new ApiParameterDefinition
                {
                    Name = "id",
                    Location = "path",
                    Required = true,
                    SchemaType = "integer",
                    SchemaFormat = "int32"
                }
            ]
        };

        var metadata = new KiotaClientMetadata
        {
            Groups =
            [
                new KiotaGroupMetadata
                {
                    GroupName = "Reports",
                    BuilderAccessExpression = "Reports",
                    Operations =
                    [
                        new KiotaOperationMetadata
                        {
                            HttpMethod = "POST",
                            EndpointPath = "reports/{param}",
                            RequestBodyTypeName = "Fake.Client.Reports.ReportsPostRequestBody"
                        }
                    ]
                }
            ]
        };

        var method = typeof(SolutionCompositionService).GetMethod(
            "BuildFacadeMethodParameters",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(method);

        var dtoContext = CreateDtoContext();
        AddDtoMapping(dtoContext, "Fake.Client.Reports.ReportsPostRequestBody", "ReportsPostRequestBodyDto");

        var parameters = (string?)method!.Invoke(
            null,
            [ "Reports", endpoint, metadata, dtoContext, AuthenticationMode.AccessTokenAccessor ]);

        Assert.Equal(
            "int? id, ReportsPostRequestBodyDto request, CancellationToken cancellationToken = default",
            parameters);
    }

    [Fact]
    public void RewriteKiotaClientNamespaces_RewritesOldApiClientNamespaceFamilyToEmbeddedNamespace()
    {
        var tempRoot = CreateTempDirectory();

        try
        {
            var clientFilePath = Path.Combine(tempRoot, "AlphaSquadLmtApplicationApiClient.cs");

            File.WriteAllText(
                clientFilePath,
                """
                using AlphaSquad.ApiClient.Api;

                namespace AlphaSquad.ApiClient;

                public partial class AlphaSquadLmtApplicationApiClient
                {
                    public global::AlphaSquad.ApiClient.Api.ApiRequestBuilder Api => throw null!;
                }
                """);

            var request = new ModernizationRequest(
                new SpecificationSource(SpecificationSourceType.File, Path.Combine(tempRoot, "openapi.json")),
                new ProjectName("GoldenSample"),
                new NamespaceName("Golden.Sample"),
                "net10.0",
                GenerationMode.Embedded,
                AuthenticationMode.AccessTokenAccessor,
                new EmbeddedProjectPrefix("AlphaSquad"));

            var metadata = new KiotaClientMetadata
            {
                RootNamespace = "AlphaSquad.ApiClient.Api"
            };

            var method = typeof(SolutionCompositionService).GetMethod(
                "RewriteKiotaClientNamespaces",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.NotNull(method);

            method!.Invoke(
                null,
                [tempRoot, request, metadata, "AlphaSquad.Lmt.Application.ApiClient"]);

            var rewrittenContent = File.ReadAllText(clientFilePath);

            Assert.DoesNotContain("AlphaSquad.ApiClient", rewrittenContent, StringComparison.Ordinal);
            Assert.Contains("namespace AlphaSquad.Lmt.Application.ApiClient;", rewrittenContent, StringComparison.Ordinal);
            Assert.Contains("AlphaSquad.Lmt.Application.ApiClient", rewrittenContent, StringComparison.Ordinal);
            Assert.Contains("ApiRequestBuilder", rewrittenContent, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void RenameKiotaClientClass_RenamesDetectedKiotaRootClassWhenItIsNotNamedApiClient()
    {
        var tempRoot = CreateTempDirectory();

        try
        {
            var originalFilePath = Path.Combine(tempRoot, "AlphaSquadApiClient.cs");

            File.WriteAllText(
                originalFilePath,
                """
                namespace AlphaSquad.Lmt.Application.ApiClient;

                public partial class AlphaSquadApiClient
                {
                    public AlphaSquadApiClient(object requestAdapter)
                    {
                    }
                }
                """);

            var method = typeof(SolutionCompositionService).GetMethod(
                "RenameKiotaClientClass",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.NotNull(method);

            method!.Invoke(
                null,
                [tempRoot, "AlphaSquadApiClient", "AlphaSquadLmtApplicationApiClient"]);

            var renamedFilePath = Path.Combine(tempRoot, "AlphaSquadLmtApplicationApiClient.cs");

            Assert.False(File.Exists(originalFilePath));
            Assert.True(File.Exists(renamedFilePath));

            var renamedContent = File.ReadAllText(renamedFilePath);

            Assert.Contains("public partial class AlphaSquadLmtApplicationApiClient", renamedContent, StringComparison.Ordinal);
            Assert.Contains("public AlphaSquadLmtApplicationApiClient(object requestAdapter)", renamedContent, StringComparison.Ordinal);
            Assert.DoesNotContain("public partial class AlphaSquadApiClient", renamedContent, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void BuildFacadeMethodBody_MapsCollectionWrapperResponsesToContractLists()
    {
        var method = typeof(SolutionCompositionService).GetMethod(
            "BuildFacadeMethodBody",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(method);

        var operation = new KiotaOperationMetadata
        {
            OperationId = "GetBusinesses",
            IsCollection = true,
            IsCollectionWrapper = true,
            CollectionPropertyName = "Value"
        };

        var methodBody = (string?)method!.Invoke(
            null,
            [ "List<BusinessResponseDto>?", operation, "_apiClient.Businesses.GetAsync(cancellationToken: cancellationToken)", string.Empty ]);

        Assert.NotNull(methodBody);
        Assert.Contains("var result = await _apiClient.Businesses.GetAsync(cancellationToken: cancellationToken).ConfigureAwait(false);", methodBody, StringComparison.Ordinal);
        Assert.Contains("return GeneratedDtoMapper.MapList<BusinessResponseDto>(result?.Value);", methodBody, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateGenerationManifestFile_IncludesKiotaAndContractMetadata()
    {
        var tempRootPath = Path.Combine(Path.GetTempPath(), "LegacyModernizerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRootPath);

        try
        {
            var request = new ModernizationRequest(
                new SpecificationSource(SpecificationSourceType.File, Path.Combine(tempRootPath, "openapi.json")),
                new ProjectName("ManifestSample"),
                new NamespaceName("Manifest.Sample"),
                "net8.0");

            var groups = new[]
            {
                new ApiGroupDefinition
                {
                    Name = "Businesses",
                    Endpoints =
                    [
                        new ApiEndpointDefinition
                        {
                            Path = "/v1/businesses",
                            Method = "GET"
                        }
                    ]
                }
            };

            var metadata = new KiotaClientMetadata
            {
                RootNamespace = "Fake.Client",
                ClientClassName = "ApiClient",
                Groups =
                [
                    new KiotaGroupMetadata
                    {
                        GroupName = "Businesses",
                        BuilderAccessExpression = "Businesses",
                        Operations =
                        [
                            new KiotaOperationMetadata
                            {
                                HttpMethod = "GET",
                                MethodName = "GetAsync",
                                EndpointPath = "businesses",
                                AccessExpression = string.Empty,
                                ReturnTypeName = "Fake.Client.Models.BusinessResponse",
                                IsCollection = true,
                                IsCollectionWrapper = true,
                                CollectionPropertyName = "Value"
                            }
                        ]
                    }
                ]
            };

            var dtoContext = CreateDtoContext();
            AddDtoMapping(dtoContext, "Fake.Client.Models.BusinessResponse", "BusinessResponseDto");
            var layout = ResolveProjectLayout(request);

            var method = typeof(SolutionCompositionService).GetMethod(
                "CreateGenerationManifestFile",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.NotNull(method);

            method!.Invoke(null, [ tempRootPath, request, layout, groups, metadata, dtoContext ]);

            var manifestPath = Path.Combine(tempRootPath, "generation-manifest.json");
            Assert.True(File.Exists(manifestPath));

            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var root = document.RootElement;

            Assert.Equal("ManifestSample", root.GetProperty("projectName").GetString());
            Assert.Equal("Standalone", root.GetProperty("generationMode").GetString());
            Assert.Equal("PerMethodToken", root.GetProperty("authenticationMode").GetString());
            Assert.Equal("ManifestSample", root.GetProperty("solutionName").GetString());
            Assert.Equal("ManifestSample.Core", root.GetProperty("projects").GetProperty("contracts").GetString());
            Assert.Equal("Manifest.Sample.Core", root.GetProperty("namespaces").GetProperty("contracts").GetString());

            var dtoMappings = root.GetProperty("dtoMappings");
            Assert.Equal("Fake.Client.Models.BusinessResponse", dtoMappings[0].GetProperty("sourceType").GetString());
            Assert.Equal("BusinessResponseDto", dtoMappings[0].GetProperty("dtoType").GetString());

            var endpoint = root.GetProperty("groups")[0].GetProperty("endpoints")[0];
            var kiota = endpoint.GetProperty("kiota");
            var contracts = endpoint.GetProperty("contracts");

            Assert.True(kiota.GetProperty("isCollection").GetBoolean());
            Assert.True(kiota.GetProperty("isCollectionWrapper").GetBoolean());
            Assert.Equal("Value", kiota.GetProperty("collectionPropertyName").GetString());
            Assert.Equal("List<BusinessResponseDto>?", contracts.GetProperty("returnType").GetString());
        }
        finally
        {
            if (Directory.Exists(tempRootPath))
                Directory.Delete(tempRootPath, recursive: true);
        }
    }

    [Fact]
    public void CreateIntegrationManifestFile_IncludesEmbeddedIntegrationMetadata()
    {
        var tempRootPath = Path.Combine(Path.GetTempPath(), "LegacyModernizerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRootPath);

        try
        {
            var request = new ModernizationRequest(
                new SpecificationSource(SpecificationSourceType.File, Path.Combine(tempRootPath, "openapi.json")),
                new ProjectName("AlphaSquad"),
                new NamespaceName("AlphaSquad"),
                "net10.0",
                GenerationMode.Embedded,
                AuthenticationMode.AccessTokenAccessor,
                new EmbeddedProjectPrefix("AlphaSquad"));

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
                            Method = "POST"
                        }
                    ]
                }
            };

            var layout = ResolveProjectLayout(request);

            var method = typeof(SolutionCompositionService).GetMethod(
                "CreateIntegrationManifestFile",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.NotNull(method);

            method!.Invoke(null, [ tempRootPath, request, layout, groups ]);

            var manifestPath = Path.Combine(tempRootPath, "integration-manifest.json");
            Assert.True(File.Exists(manifestPath));

            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var root = document.RootElement;

            Assert.Equal("Embedded", root.GetProperty("generationMode").GetString());
            Assert.Equal("AccessTokenAccessor", root.GetProperty("authenticationMode").GetString());
            Assert.Equal("AlphaSquad", root.GetProperty("projectPrefix").GetString());
            Assert.Equal("AlphaSquad.Lmt.Application.Http", root.GetProperty("projects").GetProperty("http").GetString());
            Assert.Equal(
                "AlphaSquad.Lmt.Application.Http.DependencyInjection.ServiceCollectionExtensions",
                root.GetProperty("entrypoints").GetProperty("serviceCollectionExtension").GetString());
            Assert.Equal(
                "AlphaSquad.Lmt.Application.Contracts.Interfaces.IAccessTokenAccessor",
                root.GetProperty("entrypoints").GetProperty("accessTokenAccessorInterface").GetString());
            Assert.Equal(
                "AddGeneratedApi",
                root.GetProperty("consumerGuidance").GetProperty("addGeneratedApiMethod").GetString());
            Assert.True(root.GetProperty("consumerGuidance").GetProperty("requiresAccessTokenAccessor").GetBoolean());
            Assert.Equal(
                "IAuthenticationService",
                root.GetProperty("consumerGuidance").GetProperty("apiGroupServices")[0].GetString());
        }
        finally
        {
            if (Directory.Exists(tempRootPath))
                Directory.Delete(tempRootPath, recursive: true);
        }
    }

    [Fact]
    public void ExtractEnumMembers_IgnoresEnumMemberAttributesAndReadsOnlyEnumValues()
    {
        var method = typeof(SolutionCompositionService).GetMethod(
            "ExtractEnumMembers",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(method);

        var members = (string[]?)method!.Invoke(
            null,
            [
                """
                [global::System.Runtime.Serialization.EnumMember(Value = "google")]
                Google,

                [global::System.Runtime.Serialization.EnumMember(Value = "apple")]
                Apple,
                """
            ]);

        Assert.NotNull(members);
        Assert.Equal(["Google", "Apple"], members);
    }
}
