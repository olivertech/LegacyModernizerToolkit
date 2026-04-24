namespace LegacyModernizer.Application.DTOs.Requests;

public sealed class GenerateModernizedClientRequest
{
    public string SpecificationUrl { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string BaseNamespace { get; set; } = string.Empty;
    public string TargetFramework { get; set; } = "net8.0";
}
