namespace Core.OS.EnvironmentOverrides.Extensions;

internal static class IServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        internal IServiceCollection AddEnvironmentOverrides()
        {
            services.AddTransient<IEnvironmentOverridesRepository, EnvironmentOverridesRepository>();

            return services;
        }
    }
}
