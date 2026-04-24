namespace LegacyModernizer.Application.DTOs.Commons;

public sealed class ApiEndpointDefinition
{
    public string Path { get; init; } = string.Empty;
    public string Method { get; init; } = string.Empty;
    public string OperationId { get; init; } = string.Empty;

    public List<ApiParameterDefinition> Parameters { get; init; } = new();

    public bool HasRequestBody { get; init; }
    public bool RequiresAuthorization { get; init; }
}
