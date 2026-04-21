namespace LegacyModernizer.Application.Contracts.Generations.Models;

public sealed class KiotaExecutionResult
{
    public bool Success { get; init; }
    public int ExitCode { get; init; }
    public string StandardOutput { get; init; } = string.Empty;
    public string StandardError { get; init; } = string.Empty;
}
