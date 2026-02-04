using Blazor.Shared.NavTiles.Services;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.NavTiles.Extensions;
using Sdk.Client.NavTiles.Services;

namespace Blazor.Shared.NavTiles.Extensions;

internal static class IServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddNavTiles()
        {
            services.AddNavTilesInfrastructure();

            services.AddNavTiles<SharedClientModule>();

            return services;
        }

        internal IServiceCollection AddNavTilesInfrastructure()
            => services.AddScoped<INavTileRegistryFactory, NavTileRegistryFactory>();
    }
}
