namespace LegacyModernizer.Application.Contracts.Generations;

public interface IApiGroupingService
{
    Task<IReadOnlyCollection<ApiGroupDefinition>> GetGroupsAsync(ApiSpecification specification,
                                                                 CancellationToken cancellationToken = default);
}
