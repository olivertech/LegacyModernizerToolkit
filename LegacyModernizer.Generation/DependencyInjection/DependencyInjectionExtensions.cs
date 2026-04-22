namespace LegacyModernizer.Generation.DependencyInjection;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddGeneration(this IServiceCollection services)
    {
        services.AddScoped<ISpecificationValidationService, SpecificationValidationService>();
        services.AddScoped<IClientGenerationService, ClientGenerationService>();
        services.AddScoped<ISolutionCompositionService, SolutionCompositionService>();
        services.AddScoped<IApiGroupingService, ApiGroupingService>();

        return services;
    }
}
