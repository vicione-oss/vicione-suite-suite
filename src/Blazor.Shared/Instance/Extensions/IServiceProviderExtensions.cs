using Blazor.Shared.Instance.ControlPanels.Instances;
using Blazor.Shared.Instance.ControlPanels.Instances.Services;
using Blazor.Shared.Services;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Authorization;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Services;
using Sdk.Instance;

namespace Blazor.Shared.Instance.Extensions;

public static class IServiceProviderExtensions
{
    /// <summary>
    /// Adds <see cref="InstancesControlPanel">Instances</see> to settings <see cref="ControlPanelSystemCategoryDescriptor">category</see> if Core.OS is not a standalone system
    /// </summary>
    public static IServiceProvider UseInstanceManagement(this IServiceProvider services)
    {
        var informationProvider = services.GetRequiredService<IInstanceInformationProvider>();
        var registry = services.GetRequiredService<IControlPanelRegistry<SharedClientModule>>();

        var accessLevelAuthorizationRequirement = new AccessLevelAuthorizationRequirement(SharedClientModule.ModuleId, AccessLevel.Full);

        if (informationProvider.Local.Type == InstanceType.Standalone)
        {
            registry.Add<InstanceControlPanel, InstanceControlPanelState>(
                new InstanceControlPanelDescriptor { ShowInNavigation = true },
                new InstanceControlPanelState() { InstanceId = informationProvider.Local.Id },
                new ControlPanelSystemCategoryDescriptor(),
                authorizationRequirement: accessLevelAuthorizationRequirement
            );

#if DEBUG
            registry.Add<InstancesControlPanel, InstancesControlPanelState>(
                new InstancesControlPanelDescriptor(),
                new InstancesControlPanelState(),
                new ControlPanelSystemCategoryDescriptor(),
                authorizationRequirement: accessLevelAuthorizationRequirement
            );
#endif
        }
        else
        {
            registry.Add<InstancesControlPanel, InstancesControlPanelState>(
                new InstancesControlPanelDescriptor(),
                new InstancesControlPanelState(),
                new ControlPanelSystemCategoryDescriptor(),
                authorizationRequirement: accessLevelAuthorizationRequirement
            );

            registry.Add<InstanceControlPanel, InstanceControlPanelState>(
                new InstanceControlPanelDescriptor(),
                new InstanceControlPanelState(),
                new ControlPanelSystemCategoryDescriptor(),
                authorizationRequirement: accessLevelAuthorizationRequirement
            );
        }

        if (informationProvider.Local.InRecoveryMode)
        {
            var bannerService = services.GetRequiredService<IMessageBannerService>();
            bannerService.ShowMessageBanner(Sdk.MessageBanner.Contracts.MessageType.Error, Localization.Common.RecoveryModeBannerContent);
        }

        return services;
    }
}
