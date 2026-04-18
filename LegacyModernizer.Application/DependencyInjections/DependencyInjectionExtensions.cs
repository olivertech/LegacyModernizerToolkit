namespace LegacyModernizer.Application.DependencyInjections
{
    public static class DependencyInjectionExtensions
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddScoped<IGenerateModernizedClientUseCase, GenerateModernizedClientUseCase>();

            return services;
        }
    }
}
