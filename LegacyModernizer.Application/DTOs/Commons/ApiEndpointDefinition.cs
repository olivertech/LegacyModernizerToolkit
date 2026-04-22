namespace LegacyModernizer.Application.DTOs.Commons;

public sealed class ApiEndpointDefinition
{
    public string Path { get; init; } = string.Empty;
    public string Method { get; init; } = string.Empty;
    public string OperationId { get; init; } = string.Empty;
}
