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
            [ "Notifications", endpoint, metadata, dtoContext ]);

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
            [ "Notifications", endpoint, metadata, dtoContext ]);

        Assert.Equal("int? id, CancellationToken cancellationToken = default", parameters);
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
            [ "Reports", endpoint, metadata, dtoContext ]);

        Assert.Equal(
            "int? id, ReportsPostRequestBodyDto request, bool? includeArchived, string? xCorrelationId = null, string? accessToken = null, CancellationToken cancellationToken = default",
            parameters);
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

            var method = typeof(SolutionCompositionService).GetMethod(
                "CreateGenerationManifestFile",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.NotNull(method);

            method!.Invoke(null, [ tempRootPath, request, groups, metadata, dtoContext ]);

            var manifestPath = Path.Combine(tempRootPath, "generation-manifest.json");
            Assert.True(File.Exists(manifestPath));

            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var root = document.RootElement;

            Assert.Equal("ManifestSample", root.GetProperty("projectName").GetString());

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
