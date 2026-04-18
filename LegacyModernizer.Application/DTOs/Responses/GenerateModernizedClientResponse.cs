namespace LegacyModernizer.Application.DTOs.Responses;

public sealed class GenerateModernizedClientResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? PackagePath { get; set; }
    public string? SolutionRootPath { get; set; }
    public string? ExecutionId { get; set; }
}
