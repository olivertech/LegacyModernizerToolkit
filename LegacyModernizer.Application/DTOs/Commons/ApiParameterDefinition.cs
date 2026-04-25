namespace LegacyModernizer.Application.DTOs.Common;

public sealed class ApiParameterDefinition
{
    public string Name { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty; // path, query, header
    public bool Required { get; init; }
    public string SchemaType { get; init; } = string.Empty;
    public string SchemaFormat { get; init; } = string.Empty;
}
