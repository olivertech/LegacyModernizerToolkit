namespace LegacyModernizer.Application.DTOs.Commons;

public sealed class KiotaRequestBodyPropertyMetadata
{
    public string Name { get; init; } = string.Empty;
    public string TypeName { get; init; } = "string?";
    public bool IsNullable { get; init; }
}
