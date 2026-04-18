namespace LegacyModernizer.Application.Contracts.Services;

public interface IGenerateModernizedClientUseCase
{
    Task<GenerateModernizedClientResponse> ExecuteAsync(GenerateModernizedClientRequest request,
                                                        CancellationToken cancellationToken = default);
}
