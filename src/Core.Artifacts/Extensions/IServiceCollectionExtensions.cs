using Core.Artifacts.JFrog;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Backend.Artifacts;

namespace Core.Artifacts.Extensions;

public static class IServiceCollectionExtensions
{
    /// <summary>
    /// Registers the artifact repository and its dependencies in the service collection.
    /// </summary>
    /// <typeparam name="TOptionsProvider">The type that provides options for the artifact repository.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddArtifactRepository<TOptionsProvider>(this IServiceCollection services)
        where TOptionsProvider : class, IArtifactRepositoryOptionsProvider
    {
        services.AddHttpClient();
        services.AddTransient<IArtifactRepository, JFrogArtifactRepository>();
        services.AddTransient<IArtifactRepositoryOptionsProvider, TOptionsProvider>();

        return services;
    }

    /// <summary>
    /// Registers the artifact repository and its dependencies in the service collection using a factory to create the options provider.
    /// </summary>
    /// <typeparam name="TOptionsProvider">The type that provides options for the artifact repository.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <param name="implementationFactory">A factory that creates the <typeparamref name="TOptionsProvider"/> instance.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddArtifactRepository<TOptionsProvider>(this IServiceCollection services, Func<IServiceProvider, TOptionsProvider> implementationFactory)
        where TOptionsProvider : class, IArtifactRepositoryOptionsProvider
    {
        services.AddHttpClient();
        services.AddTransient<IArtifactRepository, JFrogArtifactRepository>();
        services.AddTransient<IArtifactRepositoryOptionsProvider>(implementationFactory);

        return services;
    }
}
