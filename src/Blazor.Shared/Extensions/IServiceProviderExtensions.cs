using Blazor.Shared.Connections.Services;
using Blazor.Shared.Help.Extensions;
using Blazor.Shared.Instance.Extensions;
using Blazor.Shared.Mqtt.Extensions;
using Blazor.Shared.Onboarding.Extensions;
using Blazor.Shared.Services;
using Blazor.Shared.Settings.DateAndTime.Services;
using Blazor.Shared.UserManagement.Extensions;
using Core.Shared.HostManagement.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Blazor.Shared.Extensions;

public static class IServiceProviderExtensions
{
    extension(IServiceProvider serviceProvider)
    {
        public IServiceProvider UseSharedServices()
        {
            serviceProvider.UseHelp();
            serviceProvider.UseInstanceManagement();
            serviceProvider.UseOnboarding();

#if DEBUG
            serviceProvider.UseMqttViewerNavTile();
            serviceProvider.UseUserManagementNavTiles();
#endif

            return serviceProvider;
        }

        public async Task InitializeSharedServices(CancellationToken cancellationToken = default)
        {
            await serviceProvider.GetRequiredService<ITimeZoneDescriptorProvider>().GetAll(cancellationToken); // initializes the timezones for the scope 
            await serviceProvider.GetRequiredService<IClientTimeProvider>().Initialize(cancellationToken);
            await serviceProvider.GetRequiredService<ISuiteConnectionService>().Initialize(cancellationToken);
            await serviceProvider.GetRequiredService<ISystemConfigurationService>().Initialize(cancellationToken);
        }
    }
}
