namespace LegacyModernizer.Application.DTOs.Common;

public sealed class KiotaOperationMetadata
{
    public string OperationId { get; init; } = string.Empty;
    public string MethodName { get; init; } = string.Empty;
    public string HttpMethod { get; init; } = string.Empty;

    public string ReturnTypeName { get; init; } = "object?";
    public string RequestBodyTypeName { get; init; } = "object?";

    public string AccessExpression { get; init; } = string.Empty;

    public List<KiotaRequestBodyPropertyMetadata> RequestBodyProperties { get; init; } = new();

    public List<KiotaPathParameterMetadata> PathParameters { get; init; } = new();
}
