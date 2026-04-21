namespace LegacyModernizer.Application.Contracts.Generations
{
    public interface IKiotaRunner
    {
        Task<KiotaExecutionResult> ExecuteAsync(KiotaGenerationRequest request,
                                                CancellationToken cancellationToken = default);
    }
}
