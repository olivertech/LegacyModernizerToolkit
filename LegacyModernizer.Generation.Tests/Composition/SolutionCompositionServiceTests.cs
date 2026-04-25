using System.Reflection;
using LegacyModernizer.Application.DTOs.Common;
using LegacyModernizer.Application.DTOs.Commons;
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
