using Sdk.Client.Modules;
using Sdk.Client.NavTiles.Services;

namespace Blazor.Shared.NavTiles.Services;

internal sealed class NavTileRegistryFactory : INavTileRegistryFactory
{
    public INavTileRegistry<TClientModule> CreateNavTileRegistry<TClientModule>() where TClientModule : class, IClientModule
        => new NavTileRegistry<TClientModule>();
}
