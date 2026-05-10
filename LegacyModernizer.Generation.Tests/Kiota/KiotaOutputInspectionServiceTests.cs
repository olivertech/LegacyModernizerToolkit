using System.Reflection;
using LegacyModernizer.Application.DTOs.Commons;
using LegacyModernizer.Application.DTOs.Common;
using LegacyModernizer.Domain.Entities;
using LegacyModernizer.Domain.Enums;
using LegacyModernizer.Domain.ValueObjects;
using LegacyModernizer.Generation.Kiota;

namespace LegacyModernizer.Generation.Tests.Kiota;

public sealed class KiotaOutputInspectionServiceTests : IDisposable
{
    private readonly string _clientRootPath;

    public KiotaOutputInspectionServiceTests()
    {
        _clientRootPath = Path.Combine(Path.GetTempPath(), "LegacyModernizerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_clientRootPath);
    }

    [Fact]
    public async Task InspectAsync_UsesTheCorrectItemRequestBuilderForGetAndDeleteOperations()
    {
        WriteFile(
            "ApiClient.cs",
            """
            namespace Fake.Client;

            public partial class FakeApiClient
            {
            }
            """);

        WriteFile(
            Path.Combine("Calendar", "CalendarRequestBuilder.cs"),
            """
            namespace Fake.Client.Calendar;

            public class CalendarRequestBuilder
            {
                [global::System.Obsolete("deprecated")]
                public global::Fake.Client.Calendar.Item.ItemRequestBuilder this[string position] => throw null!;
            }
            """);

        WriteFile(
            Path.Combine("Calendar", "Item", "ItemRequestBuilder.cs"),
            """
            namespace Fake.Client.Calendar.Item;

            public class ItemRequestBuilder
            {
                public async global::System.Threading.Tasks.Task<global::Fake.Client.Models.CalendarEntryResponse?> GetAsync(global::System.Action<object>? requestConfiguration = default, global::System.Threading.CancellationToken cancellationToken = default)
                {
                    await global::System.Threading.Tasks.Task.CompletedTask;
                    return default;
                }

                public async global::System.Threading.Tasks.Task DeleteAsync(global::System.Action<object>? requestConfiguration = default, global::System.Threading.CancellationToken cancellationToken = default)
                {
                    await global::System.Threading.Tasks.Task.CompletedTask;
                }
            }
            """);

        WriteFile(
            Path.Combine("Profiles", "ProfilesRequestBuilder.cs"),
            """
            namespace Fake.Client.Profiles;

            public class ProfilesRequestBuilder
            {
                public global::Fake.Client.Profiles.Item.ItemRequestBuilder this[string position] => throw null!;
            }
            """);

        WriteFile(
            Path.Combine("Profiles", "Item", "ItemRequestBuilder.cs"),
            """
            namespace Fake.Client.Profiles.Item;

            public class ItemRequestBuilder
            {
                public async global::System.Threading.Tasks.Task<global::Fake.Client.Models.ProfileResponse?> GetAsync(global::System.Action<object>? requestConfiguration = default, global::System.Threading.CancellationToken cancellationToken = default)
                {
                    await global::System.Threading.Tasks.Task.CompletedTask;
                    return default;
                }
            }
            """);

        WriteFile(
            Path.Combine("Models", "CalendarEntryResponse.cs"),
            """
            namespace Fake.Client.Models;

            public class CalendarEntryResponse
            {
                public string? Id { get; set; }
            }
            """);

        WriteFile(
            Path.Combine("Models", "ProfileResponse.cs"),
            """
            namespace Fake.Client.Models;

            public class ProfileResponse
            {
                public string? Id { get; set; }
            }
            """);

        var artifact = new GeneratedArtifact(
            ArtifactType.GeneratedClient,
            new ArtifactLocation(_clientRootPath),
            ExecutionStep.ClientGeneration);

        var apiGroups = new[]
        {
            new ApiGroupDefinition
            {
                Name = "Calendar",
                Endpoints =
                [
                    new ApiEndpointDefinition
                    {
                        Path = "/v1/calendar/{id}",
                        Method = "GET",
                        Parameters =
                        [
                            new ApiParameterDefinition { Name = "id", Location = "path", Required = true }
                        ]
                    },
                    new ApiEndpointDefinition
                    {
                        Path = "/v1/calendar/{id}",
                        Method = "DELETE",
                        Parameters =
                        [
                            new ApiParameterDefinition { Name = "id", Location = "path", Required = true }
                        ]
                    }
                ]
            }
        };

        var service = new KiotaOutputInspectionService();

        var metadata = await service.InspectAsync(artifact, apiGroups);
        var calendarGroup = Assert.Single(metadata.Groups, x => x.GroupName == "Calendar");

        var getOperation = Assert.Single(calendarGroup.Operations, x => x.HttpMethod == "GET");
        Assert.Equal("Fake.Client.Models.CalendarEntryResponse?", getOperation.ReturnTypeName);
        Assert.Equal("calendar/{param}", getOperation.EndpointPath);
        var getPathParameter = Assert.Single(getOperation.PathParameters);
        Assert.Equal("id", getPathParameter.Name, ignoreCase: true);
        Assert.Equal("[id]", getPathParameter.AccessExpression);

        var deleteOperation = Assert.Single(calendarGroup.Operations, x => x.HttpMethod == "DELETE");
        Assert.Equal(string.Empty, deleteOperation.ReturnTypeName);
        var deletePathParameter = Assert.Single(deleteOperation.PathParameters);
        Assert.Equal("[id]", deletePathParameter.AccessExpression);
    }

    [Fact]
    public async Task InspectAsync_ExtractsTypedIndexerParameterType_WhenStringIndexerIsObsolete()
    {
        WriteFile(
            "ApiClient.cs",
            """
            namespace Fake.Client;

            public partial class FakeApiClient
            {
            }
            """);

        WriteFile(
            Path.Combine("Notifications", "NotificationsRequestBuilder.cs"),
            """
            namespace Fake.Client.Notifications;

            public class NotificationsRequestBuilder
            {
                [global::System.Obsolete("deprecated")]
                public global::Fake.Client.Notifications.Item.ItemRequestBuilder this[string position] => throw null!;

                public global::Fake.Client.Notifications.Item.ItemRequestBuilder this[int? position] => throw null!;
            }
            """);

        WriteFile(
            Path.Combine("Notifications", "Item", "ItemRequestBuilder.cs"),
            """
            namespace Fake.Client.Notifications.Item;

            public class ItemRequestBuilder
            {
                public async global::System.Threading.Tasks.Task<global::Fake.Client.Models.NotificationResponse?> GetAsync(global::System.Action<object>? requestConfiguration = default, global::System.Threading.CancellationToken cancellationToken = default)
                {
                    await global::System.Threading.Tasks.Task.CompletedTask;
                    return default;
                }
            }
            """);

        WriteFile(
            Path.Combine("Models", "NotificationResponse.cs"),
            """
            namespace Fake.Client.Models;

            public class NotificationResponse
            {
                public string? Id { get; set; }
            }
            """);

        var artifact = new GeneratedArtifact(
            ArtifactType.GeneratedClient,
            new ArtifactLocation(_clientRootPath),
            ExecutionStep.ClientGeneration);

        var apiGroups = new[]
        {
            new ApiGroupDefinition
            {
                Name = "Notifications",
                Endpoints =
                [
                    new ApiEndpointDefinition
                    {
                        Path = "/v1/notifications/{id}",
                        Method = "GET",
                        Parameters =
                        [
                            new ApiParameterDefinition { Name = "id", Location = "path", Required = true }
                        ]
                    }
                ]
            }
        };

        var service = new KiotaOutputInspectionService();

        var metadata = await service.InspectAsync(artifact, apiGroups);
        var notificationsGroup = Assert.Single(metadata.Groups, x => x.GroupName == "Notifications");
        Assert.Equal("int?", notificationsGroup.DefaultPathParameterTypeName);
        Assert.Equal("[{parameterName}]", notificationsGroup.DefaultPathAccessExpressionTemplate);
        var getOperation = Assert.Single(notificationsGroup.Operations, x => x.HttpMethod == "GET");
        var pathParameter = Assert.Single(getOperation.PathParameters);

        Assert.Equal("int?", pathParameter.TypeName);
        Assert.Equal("[id]", pathParameter.AccessExpression);
    }

    [Fact]
    public async Task InspectAsync_UsesTypedPathParameterObjectIndexer_WhenAvailable()
    {
        WriteFile(
            "ApiClient.cs",
            """
            namespace Fake.Client;

            public partial class FakeApiClient
            {
            }
            """);

        WriteFile(
            Path.Combine("Businesses", "BusinessesRequestBuilder.cs"),
            """
            namespace Fake.Client.Businesses;

            public class BusinessesRequestBuilder
            {
                [global::System.Obsolete("deprecated")]
                public global::Fake.Client.Businesses.Item.ItemRequestBuilder this[string position] => throw null!;

                public global::Fake.Client.Businesses.Item.ItemRequestBuilder this[global::Fake.Client.Businesses.BusinessesRequestBuilderPathParameters pathParameters] => throw null!;
            }
            """);

        WriteFile(
            Path.Combine("Businesses", "BusinessesRequestBuilderPathParameters.cs"),
            """
            namespace Fake.Client.Businesses;

            public class BusinessesRequestBuilderPathParameters
            {
                public string? Id { get; set; }
            }
            """);

        WriteFile(
            Path.Combine("Businesses", "Item", "ItemRequestBuilder.cs"),
            """
            namespace Fake.Client.Businesses.Item;

            public class ItemRequestBuilder
            {
                public async global::System.Threading.Tasks.Task<global::Fake.Client.Models.BusinessResponse?> GetAsync(global::System.Action<object>? requestConfiguration = default, global::System.Threading.CancellationToken cancellationToken = default)
                {
                    await global::System.Threading.Tasks.Task.CompletedTask;
                    return default;
                }
            }
            """);

        WriteFile(
            Path.Combine("Models", "BusinessResponse.cs"),
            """
            namespace Fake.Client.Models;

            public class BusinessResponse
            {
                public string? Id { get; set; }
            }
            """);

        var artifact = new GeneratedArtifact(
            ArtifactType.GeneratedClient,
            new ArtifactLocation(_clientRootPath),
            ExecutionStep.ClientGeneration);

        var apiGroups = new[]
        {
            new ApiGroupDefinition
            {
                Name = "Businesses",
                Endpoints =
                [
                    new ApiEndpointDefinition
                    {
                        Path = "/v1/businesses/{id}",
                        Method = "GET",
                        Parameters =
                        [
                            new ApiParameterDefinition { Name = "id", Location = "path", Required = true }
                        ]
                    }
                ]
            }
        };

        var service = new KiotaOutputInspectionService();

        var metadata = await service.InspectAsync(artifact, apiGroups);
        var businessesGroup = Assert.Single(metadata.Groups, x => x.GroupName == "Businesses");
        var getOperation = Assert.Single(businessesGroup.Operations, x => x.HttpMethod == "GET");
        var pathParameter = Assert.Single(getOperation.PathParameters);

        Assert.Equal("string?", pathParameter.TypeName);
        Assert.Equal("[new Fake.Client.Businesses.BusinessesRequestBuilderPathParameters { Id = id }]", pathParameter.AccessExpression);
    }

    [Fact]
    public void FindByMethodForParameter_IgnoresRequestBuilderConstructors()
    {
        var content =
            """
            namespace Fake.Client.Tenants;

            public class TenantsRequestBuilder
            {
                public global::Fake.Client.Tenants.Slug.SlugRequestBuilder BySlug(string slug) => throw null!;
            }

            namespace Fake.Client.Tenants.Slug;

            public class SlugRequestBuilder
            {
                public SlugRequestBuilder(global::System.Collections.Generic.Dictionary<string, object> pathParameters, global::Microsoft.Kiota.Abstractions.IRequestAdapter requestAdapter)
                {
                }
            }
            """;

        var method = typeof(KiotaOutputInspectionService).GetMethod(
            "FindByMethodForParameter",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(method);

        var result = method!.Invoke(null, [content, "slug", "slug"]);
        Assert.NotNull(result);

        var methodName = (string?)result!.GetType().GetField("Item1")?.GetValue(result);
        var parameterTypeName = (string?)result.GetType().GetField("Item2")?.GetValue(result);

        Assert.Equal("BySlug", methodName);
        Assert.Equal("string", parameterTypeName);
    }

    [Fact]
    public async Task InspectAsync_DetectsCollectionWrapperResponsesUsingValueProperty()
    {
        WriteFile(
            "ApiClient.cs",
            """
            namespace Fake.Client;

            public partial class FakeApiClient
            {
            }
            """);

        WriteFile(
            Path.Combine("Businesses", "BusinessesRequestBuilder.cs"),
            """
            namespace Fake.Client.Businesses;

            public class BusinessesRequestBuilder
            {
                public async global::System.Threading.Tasks.Task<global::Fake.Client.Models.BusinessPageResponse?> GetAsync(global::System.Action<object>? requestConfiguration = default, global::System.Threading.CancellationToken cancellationToken = default)
                {
                    await global::System.Threading.Tasks.Task.CompletedTask;
                    return default;
                }
            }
            """);

        WriteFile(
            Path.Combine("Models", "BusinessPageResponse.cs"),
            """
            namespace Fake.Client.Models;

            public class BusinessPageResponse
            {
                public global::System.Collections.Generic.List<global::Fake.Client.Models.BusinessResponse>? Value { get; set; }
            }
            """);

        WriteFile(
            Path.Combine("Models", "BusinessResponse.cs"),
            """
            namespace Fake.Client.Models;

            public class BusinessResponse
            {
                public string? Id { get; set; }
            }
            """);

        var artifact = new GeneratedArtifact(
            ArtifactType.GeneratedClient,
            new ArtifactLocation(_clientRootPath),
            ExecutionStep.ClientGeneration);

        var apiGroups = new[]
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

        var service = new KiotaOutputInspectionService();

        var metadata = await service.InspectAsync(artifact, apiGroups);
        var businessesGroup = Assert.Single(metadata.Groups, x => x.GroupName == "Businesses");
        var operation = Assert.Single(businessesGroup.Operations, x => x.HttpMethod == "GET");

        Assert.Equal("Fake.Client.Models.BusinessResponse", operation.ReturnTypeName);
        Assert.True(operation.IsCollection);
        Assert.True(operation.IsCollectionWrapper);
        Assert.Equal("Value", operation.CollectionPropertyName);
    }

    [Fact]
    public async Task InspectAsync_ExtractsRequestBodyPropertiesIgnoringFrameworkProperties()
    {
        WriteFile(
            "ApiClient.cs",
            """
            namespace Fake.Client;

            public partial class FakeApiClient
            {
            }
            """);

        WriteFile(
            Path.Combine("Authentication", "Login", "LoginRequestBuilder.cs"),
            """
            namespace Fake.Client.Authentication.Login;

            public class LoginRequestBuilder
            {
                public async global::System.Threading.Tasks.Task<global::Fake.Client.Models.AuthResponse?> PostAsync(global::Fake.Client.Authentication.Login.LoginPostRequestBody body, global::System.Action<object>? requestConfiguration = default, global::System.Threading.CancellationToken cancellationToken = default)
                {
                    await global::System.Threading.Tasks.Task.CompletedTask;
                    return default;
                }
            }
            """);

        WriteFile(
            Path.Combine("Authentication", "Login", "LoginPostRequestBody.cs"),
            """
            namespace Fake.Client.Authentication.Login;

            public class LoginPostRequestBody
            {
                public string? Email { get; set; }
                public global::Fake.Client.Authentication.Login.LoginPostRequestBody_provider? Provider { get; set; }
                public global::System.Collections.Generic.IDictionary<string, object>? AdditionalData { get; set; }
                public object? BackingStore { get; set; }
            }
            """);

        WriteFile(
            Path.Combine("Authentication", "Login", "LoginPostRequestBody_provider.cs"),
            """
            namespace Fake.Client.Authentication.Login;

            public enum LoginPostRequestBody_provider
            {
                Google,
                Apple,
            }
            """);

        WriteFile(
            Path.Combine("Models", "AuthResponse.cs"),
            """
            namespace Fake.Client.Models;

            public class AuthResponse
            {
                public string? AccessToken { get; set; }
            }
            """);

        var artifact = new GeneratedArtifact(
            ArtifactType.GeneratedClient,
            new ArtifactLocation(_clientRootPath),
            ExecutionStep.ClientGeneration);

        var apiGroups = new[]
        {
            new ApiGroupDefinition
            {
                Name = "Login",
                Endpoints =
                [
                    new ApiEndpointDefinition
                    {
                        Path = "/v1/authentication/login",
                        Method = "POST",
                        OperationId = "Login",
                        HasRequestBody = true
                    }
                ]
            }
        };

        var service = new KiotaOutputInspectionService();

        var metadata = await service.InspectAsync(artifact, apiGroups);
        var loginGroup = Assert.Single(metadata.Groups, x => x.GroupName == "Login");
        var operation = Assert.Single(loginGroup.Operations, x => x.HttpMethod == "POST");

        Assert.Equal("Fake.Client.Authentication.Login.LoginPostRequestBody", operation.RequestBodyTypeName);
        Assert.Collection(
            operation.RequestBodyProperties,
            property =>
            {
                Assert.Equal("Email", property.Name);
                Assert.Equal("string?", property.TypeName);
                Assert.True(property.IsNullable);
            },
            property =>
            {
                Assert.Equal("Provider", property.Name);
                Assert.Equal("Fake.Client.Authentication.Login.LoginPostRequestBody_provider?", property.TypeName);
                Assert.True(property.IsNullable);
            });
    }

    private void WriteFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(_clientRootPath, relativePath);
        var directoryPath = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrWhiteSpace(directoryPath))
            Directory.CreateDirectory(directoryPath);

        File.WriteAllText(fullPath, content.Replace("\r\n", "\n").Replace("\n", Environment.NewLine));
    }

    public void Dispose()
    {
        if (Directory.Exists(_clientRootPath))
            Directory.Delete(_clientRootPath, recursive: true);
    }
}
