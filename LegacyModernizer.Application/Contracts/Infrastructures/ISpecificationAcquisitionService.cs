namespace LegacyModernizer.Application.Contracts.Infrastructures;

public interface ISpecificationAcquisitionService
{
    Task<ApiSpecification> AcquireAsync(SpecificationSource source,
                                        Workspace workspace,
                                        CancellationToken cancellationToken = default);
}
