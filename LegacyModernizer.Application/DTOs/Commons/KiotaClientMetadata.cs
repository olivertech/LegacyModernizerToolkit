namespace LegacyModernizer.Application.DTOs.Common;

public sealed class KiotaClientMetadata
{
    public string RootNamespace { get; init; } = string.Empty;
    public string ClientClassName { get; init; } = string.Empty;
    public List<KiotaGroupMetadata> Groups { get; init; } = new();
}
