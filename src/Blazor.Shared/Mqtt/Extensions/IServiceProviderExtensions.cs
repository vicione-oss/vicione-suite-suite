using Microsoft.Extensions.DependencyInjection;
using Sdk.Authorization;
using Sdk.Client.NavTiles.Enums;
using Sdk.Client.NavTiles.Services;

namespace Blazor.Shared.Mqtt.Extensions;

internal static class IServiceProviderExtensions
{
    extension(IServiceProvider serviceProvider)
    {
        public void UseMqttViewerNavTile()
        {
            try
            {
                var registry = serviceProvider.GetRequiredService<INavTileRegistry<SharedClientModule>>();
                if (registry.Any(i => i.ComponentType == typeof(MqttViewerNavTile)))
                    return;

                var accessLevelAuthorizationRequirement = new AccessLevelAuthorizationRequirement(SharedClientModule.ModuleId, AccessLevel.Full);

                registry.Add<MqttViewerNavTile>(Constants.MqttViewerNavTileId, linkTarget: Constants.MqttViewerRoute, group: NavTileGroup.Administration,
                    authorizationRequirement: accessLevelAuthorizationRequirement);
            }
            catch (ObjectDisposedException)
            {
                // Can happen during shutdown.
            }
        }
    }
}
