namespace LegacyModernizer.Application.Contracts.Generations;

public interface ISpecificationValidationService
{
    Task<ApiSpecification> ValidateAsync(ApiSpecification specification,
                                         CancellationToken cancellationToken = default);
}
