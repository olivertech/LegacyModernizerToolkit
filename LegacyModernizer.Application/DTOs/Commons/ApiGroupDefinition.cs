namespace LegacyModernizer.Application.DTOs.Commons;

public sealed class ApiGroupDefinition
{
    public string Name { get; init; } = string.Empty;
    public List<ApiEndpointDefinition> Endpoints { get; init; } = new();
}
