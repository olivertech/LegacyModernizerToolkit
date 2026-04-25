using System.Reflection;
using LegacyModernizer.Application.DTOs.Common;
using LegacyModernizer.Application.DTOs.Commons;
using LegacyModernizer.Generation.Composition;

namespace LegacyModernizer.Generation.Tests.Composition;

public sealed class SolutionCompositionServiceTests
{
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

        var parameters = (string?)method!.Invoke(
            null,
            [ "Notifications", endpoint, metadata ]);

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

        var parameters = (string?)method!.Invoke(
            null,
            [ "Notifications", endpoint, metadata ]);

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
            [ "Fake.Client.Models.NotificationResponse?", null, "_apiClient.Notifications[id].GetAsync(cancellationToken: cancellationToken)" ]);

        Assert.NotNull(methodBody);
        Assert.Contains("#pragma warning disable CS0618", methodBody, StringComparison.Ordinal);
        Assert.Contains("_apiClient.Notifications[id].GetAsync", methodBody, StringComparison.Ordinal);
        Assert.Contains("#pragma warning restore CS0618", methodBody, StringComparison.Ordinal);
    }
}
