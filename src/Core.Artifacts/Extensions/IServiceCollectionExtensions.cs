using Core.Artifacts.JFrog;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Backend.Artifacts;

namespace Core.Artifacts.Extensions;

public static class IServiceCollectionExtensions
{
    /// <summary>
    /// Registers the artifact repository and its dependencies.
    /// </summary>
    public static IServiceCollection AddArtifactRepository<TOptionsProvider>(this IServiceCollection services)
        where TOptionsProvider : class, IArtifactRepositoryOptionsProvider
    {
        services.AddHttpClient();
        services.AddTransient<IArtifactRepository, JFrogArtifactRepository>();
        services.AddTransient<IArtifactRepositoryOptionsProvider, TOptionsProvider>();

        return services;
    }

    /// <summary>
    /// Registers the artifact repository and its dependencies, with the options provider built by a factory.
    /// </summary>
    public static IServiceCollection AddArtifactRepository<TOptionsProvider>(this IServiceCollection services, Func<IServiceProvider, TOptionsProvider> implementationFactory)
        where TOptionsProvider : class, IArtifactRepositoryOptionsProvider
    {
        services.AddHttpClient();
        services.AddTransient<IArtifactRepository, JFrogArtifactRepository>();
        services.AddTransient<IArtifactRepositoryOptionsProvider>(implementationFactory);

        return services;
    }
}
