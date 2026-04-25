namespace LegacyModernizer.Application.DTOs.Common;

public sealed class KiotaOperationMetadata
{
    public string OperationId { get; init; } = string.Empty;
    public string MethodName { get; init; } = string.Empty;
    public string HttpMethod { get; init; } = string.Empty;
    public string ReturnTypeName { get; init; } = string.Empty;
    public string RequestBodyTypeName { get; init; } = string.Empty;
    public string AccessExpression { get; init; } = string.Empty;
    public string EndpointPath { get; init; } = string.Empty;
    public bool IsCollection { get; init; }
    public bool IsCollectionWrapper { get; init; }
    public string? CollectionPropertyName { get; init; }

    public List<KiotaRequestBodyPropertyMetadata> RequestBodyProperties { get; init; } = new();
    public List<KiotaPathParameterMetadata> PathParameters { get; init; } = new();
}
