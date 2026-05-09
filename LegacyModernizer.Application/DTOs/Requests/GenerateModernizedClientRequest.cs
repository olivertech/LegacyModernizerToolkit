namespace LegacyModernizer.Application.DTOs.Requests;

public sealed class GenerateModernizedClientRequest
{
    public string SpecificationUrl { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string BaseNamespace { get; set; } = string.Empty;
    public string TargetFramework { get; set; } = "net8.0";
    public GenerationMode GenerationMode { get; set; } = GenerationMode.Standalone;
    public AuthenticationMode AuthenticationMode { get; set; } = AuthenticationMode.PerMethodToken;
    public string? EmbeddedProjectPrefix { get; set; }
}
