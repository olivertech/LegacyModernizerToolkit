namespace LegacyModernizer.Application.DTOs.Common;

public sealed class KiotaGroupMetadata
{
    public string GroupName { get; init; } = string.Empty;
    public string BuilderTypeName { get; init; } = string.Empty;
    public string BuilderPropertyName { get; init; } = string.Empty;
    public string BuilderAccessExpression { get; init; } = string.Empty;
}