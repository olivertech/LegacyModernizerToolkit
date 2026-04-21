namespace LegacyModernizer.Application.Contracts.Generations.Models;

public sealed class KiotaGenerationRequest
{
    public string SpecificationPath { get; init; } = string.Empty;
    public string OutputPath { get; init; } = string.Empty;
    public string ClientNamespace { get; init; } = string.Empty;
    public string Language { get; init; } = "csharp"; // Default sendo C#, mas pode ser parametrizado para outras linguagens suportadas pelo Kiota no futuro
}
