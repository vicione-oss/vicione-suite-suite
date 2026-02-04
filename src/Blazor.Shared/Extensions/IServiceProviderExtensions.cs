using Blazor.Shared.Connections.Services;
using Blazor.Shared.Help.Extensions;
using Blazor.Shared.Instance.Extensions;
using Blazor.Shared.Mqtt;
using Blazor.Shared.Onboarding.Extensions;
using Blazor.Shared.Services;
using Blazor.Shared.Settings.DateAndTime.Services;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Authorization;
using Sdk.Client.NavTiles.Enums;
using Sdk.Client.NavTiles.Services;

namespace Blazor.Shared.Extensions;

public static class IServiceProviderExtensions
{
    extension(IServiceProvider services)
    {
        public IServiceProvider UseSharedServices()
        {
            services.UseHelp();
            services.UseInstanceManagement();
            services.UseOnboarding();
            services.UseMqttViewer();

            return services;
        }

        public async Task InitializeSharedServices(CancellationToken cancellationToken = default)
        {
            await services.GetRequiredService<ITimeZoneDescriptorProvider>().GetAll(cancellationToken); // initializes the timezones for the scope 
            await services.GetRequiredService<IClientTimeProvider>().Initialize(cancellationToken);
            await services.GetRequiredService<ISuiteConnectionService>().Initialize(cancellationToken);
        }

        private void UseMqttViewer()
        {
#if DEBUG
            try
            {
                var registry = services.GetRequiredService<INavTileRegistry<SharedClientModule>>();
                if (registry.Any(i => i.ComponentType == typeof(MqttViewerNavTile)))
                    return;

                var accessLevelAuthorizationRequirement = new AccessLevelAuthorizationRequirement(SharedClientModule.ModuleId, AccessLevel.Full);
                registry.Add<MqttViewerNavTile>(Constants.MqttViewerNavTileId, linkTarget: Constants.MqttViewerRoute, group: NavTileGroup.Administration,
                    authorizationRequirement: accessLevelAuthorizationRequirement);
            }
            catch (ObjectDisposedException)
            {
                // this happens...
            }
#endif
        }
    }
}
