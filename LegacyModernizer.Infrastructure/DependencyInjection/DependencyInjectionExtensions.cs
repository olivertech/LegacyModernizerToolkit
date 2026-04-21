namespace LegacyModernizer.Infrastructure.DependencyInjection;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // Workspace
        services.AddScoped<IWorkspacePreparationService, WorkspacePreparationService>();

        // Specification Acquisition
        services.AddHttpClient<ISpecificationAcquisitionService, SpecificationAcquisitionService>();

        // Kiota Runner
        services.AddScoped<IKiotaRunner, KiotaRunner>();

        // Packaging
        services.AddScoped<IPackageGenerationService, PackageGenerationService>();

        return services;
    }
}
