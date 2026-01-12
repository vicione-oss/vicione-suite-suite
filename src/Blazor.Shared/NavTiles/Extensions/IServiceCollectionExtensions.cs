using Blazor.Shared.NavTiles.Services;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.NavTiles.Extensions;
using Sdk.Client.NavTiles.Services;

namespace Blazor.Shared.NavTiles.Extensions;

internal static class IServiceCollectionExtensions
{
    public static IServiceCollection AddNavTiles(this IServiceCollection services)
    {
        services.AddNavTilesInfrastructure();

        services.AddNavTiles<SharedClientModule>();

        return services;
    }

    internal static IServiceCollection AddNavTilesInfrastructure(this IServiceCollection services)
        => services.AddScoped<INavTileRegistryFactory, NavTileRegistryFactory>();
}
