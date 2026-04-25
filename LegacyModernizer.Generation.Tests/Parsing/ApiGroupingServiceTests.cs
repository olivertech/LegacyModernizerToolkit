using LegacyModernizer.Domain.Entities;
using LegacyModernizer.Domain.Enums;
using LegacyModernizer.Domain.ValueObjects;
using LegacyModernizer.Generation.Parsing;

namespace LegacyModernizer.Generation.Tests.Parsing;

public sealed class ApiGroupingServiceTests : IDisposable
{
    private readonly string _tempRootPath;

    public ApiGroupingServiceTests()
    {
        _tempRootPath = Path.Combine(Path.GetTempPath(), "LegacyModernizerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRootPath);
    }

    [Fact]
    public async Task GetGroupsAsync_ResolvesReferencedParametersFromComponents()
    {
        var specificationPath = Path.Combine(_tempRootPath, "openapi.json");
        await File.WriteAllTextAsync(
            specificationPath,
            """
            {
              "openapi": "3.0.1",
              "paths": {
                "/v1/notifications/{id}": {
                  "parameters": [
                    { "$ref": "#/components/parameters/NotificationId" }
                  ],
                  "get": {
                    "tags": [ "Notifications" ],
                    "operationId": "GetNotification"
                  }
                }
              },
              "components": {
                "parameters": {
                  "NotificationId": {
                    "name": "id",
                    "in": "path",
                    "required": true,
                    "schema": {
                      "type": "integer",
                      "format": "int32"
                    }
                  }
                }
              }
            }
            """);

        var specification = new ApiSpecification(
            new SpecificationSource(SpecificationSourceType.File, specificationPath),
            SpecificationFormat.Json);

        specification.SetLocalPath(specificationPath);
        specification.MarkValidationStatusAsValid();

        var service = new ApiGroupingService();
        var groups = await service.GetGroupsAsync(specification);

        var group = Assert.Single(groups);
        var endpoint = Assert.Single(group.Endpoints);
        var parameter = Assert.Single(endpoint.Parameters);

        Assert.Equal("Notifications", group.Name);
        Assert.Equal("id", parameter.Name);
        Assert.Equal("path", parameter.Location);
        Assert.True(parameter.Required);
        Assert.Equal("integer", parameter.SchemaType);
        Assert.Equal("int32", parameter.SchemaFormat);
    }

    [Fact]
    public async Task GetGroupsAsync_MergesOperationLevelOverridesWithoutLosingPathParameterSchema()
    {
        var specificationPath = Path.Combine(_tempRootPath, "openapi-merged-parameters.json");
        await File.WriteAllTextAsync(
            specificationPath,
            """
            {
              "openapi": "3.0.1",
              "paths": {
                "/v1/notifications/{id}": {
                  "parameters": [
                    { "$ref": "#/components/parameters/NotificationId" }
                  ],
                  "get": {
                    "tags": [ "Notifications" ],
                    "operationId": "GetNotification",
                    "parameters": [
                      {
                        "name": "id",
                        "in": "path",
                        "required": true
                      }
                    ]
                  }
                }
              },
              "components": {
                "parameters": {
                  "NotificationId": {
                    "name": "id",
                    "in": "path",
                    "required": true,
                    "schema": {
                      "$ref": "#/components/schemas/NotificationIdSchema"
                    }
                  }
                },
                "schemas": {
                  "NotificationIdSchema": {
                    "type": "integer",
                    "format": "int32"
                  }
                }
              }
            }
            """);

        var specification = new ApiSpecification(
            new SpecificationSource(SpecificationSourceType.File, specificationPath),
            SpecificationFormat.Json);

        specification.SetLocalPath(specificationPath);
        specification.MarkValidationStatusAsValid();

        var service = new ApiGroupingService();
        var groups = await service.GetGroupsAsync(specification);

        var parameter = Assert.Single(Assert.Single(Assert.Single(groups).Endpoints).Parameters);

        Assert.Equal("integer", parameter.SchemaType);
        Assert.Equal("int32", parameter.SchemaFormat);
    }

    [Fact]
    public async Task GetGroupsAsync_ResolvesComposedSchemaTypesForQueryAndHeaderParameters()
    {
        var specificationPath = Path.Combine(_tempRootPath, "openapi-composed-parameters.json");
        await File.WriteAllTextAsync(
            specificationPath,
            """
            {
              "openapi": "3.0.1",
              "paths": {
                "/v1/reports": {
                  "get": {
                    "tags": [ "Reports" ],
                    "operationId": "GetReports",
                    "parameters": [
                      {
                        "name": "fromDate",
                        "in": "query",
                        "schema": {
                          "allOf": [
                            { "$ref": "#/components/schemas/DateTimeFilter" }
                          ]
                        }
                      },
                      {
                        "name": "includeInactive",
                        "in": "header",
                        "schema": {
                          "oneOf": [
                            { "type": "boolean" }
                          ]
                        }
                      }
                    ]
                  }
                }
              },
              "components": {
                "schemas": {
                  "DateTimeFilter": {
                    "type": "string",
                    "format": "date-time"
                  }
                }
              }
            }
            """);

        var specification = new ApiSpecification(
            new SpecificationSource(SpecificationSourceType.File, specificationPath),
            SpecificationFormat.Json);

        specification.SetLocalPath(specificationPath);
        specification.MarkValidationStatusAsValid();

        var service = new ApiGroupingService();
        var groups = await service.GetGroupsAsync(specification);

        var endpoint = Assert.Single(Assert.Single(groups).Endpoints);
        var fromDate = Assert.Single(endpoint.Parameters, x => x.Name == "fromDate");
        var includeInactive = Assert.Single(endpoint.Parameters, x => x.Name == "includeInactive");

        Assert.Equal("Reports", Assert.Single(groups).Name);
        Assert.Equal("string", fromDate.SchemaType);
        Assert.Equal("date-time", fromDate.SchemaFormat);
        Assert.Equal("boolean", includeInactive.SchemaType);
        Assert.Equal(string.Empty, includeInactive.SchemaFormat);
    }

    [Fact]
    public async Task GetGroupsAsync_HonorsGlobalSecurityAndOperationLevelOverrides()
    {
        var specificationPath = Path.Combine(_tempRootPath, "openapi-security.json");
        await File.WriteAllTextAsync(
            specificationPath,
            """
            {
              "openapi": "3.0.1",
              "security": [
                { "bearerAuth": [] }
              ],
              "paths": {
                "/v1/profile": {
                  "get": {
                    "tags": [ "Profile" ],
                    "operationId": "GetProfile"
                  }
                },
                "/v1/public-feed": {
                  "get": {
                    "tags": [ "PublicFeed" ],
                    "operationId": "GetPublicFeed",
                    "security": []
                  }
                }
              }
            }
            """);

        var specification = new ApiSpecification(
            new SpecificationSource(SpecificationSourceType.File, specificationPath),
            SpecificationFormat.Json);

        specification.SetLocalPath(specificationPath);
        specification.MarkValidationStatusAsValid();

        var service = new ApiGroupingService();
        var groups = await service.GetGroupsAsync(specification);

        var profileEndpoint = Assert.Single(groups.Single(x => x.Name == "Profile").Endpoints);
        var publicFeedEndpoint = Assert.Single(groups.Single(x => x.Name == "PublicFeed").Endpoints);

        Assert.True(profileEndpoint.RequiresAuthorization);
        Assert.False(publicFeedEndpoint.RequiresAuthorization);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRootPath))
            Directory.Delete(_tempRootPath, recursive: true);
    }
}
