using Blazor.Shared.Instance.ControlPanels;
using Blazor.Shared.Instance.Services;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.ControlPanels.Extensions;
using ViciOne.Ui.Blazor.Components.Grid.Extensions;

namespace Blazor.Shared.Instance.Extensions;

internal static class IServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddInstanceManagement()
        {
            services.AddInstancesControlPanel()
                .AddInstanceControlPanel()
                .AddUpdateControlPanel();

            return services;
        }

        public IServiceCollection AddInstancesControlPanel()
        {
            services.AddControlPanel<SharedClientModule, InstancesControlPanel, InstancesControlPanelState>()
                .WithSaveHandler<InstancesControlPanelSaveHandler>()
                .WithResetHandler<InstancesControlPanelResetHandler>();

            services.AddGridItemSelectColumn()
                .AddGridItemSelection<Guid>(typeof(InstancesControlPanelServiceKey));

            return services;
        }

        public IServiceCollection AddInstanceControlPanel()
        {
            services.AddControlPanel<SharedClientModule, InstanceControlPanel, InstanceControlPanelState>()
                .WithSaveHandler<InstanceControlPanelSaveHandler>()
                .WithResetHandler<InstanceControlPanelResetHandler>();

            return services;
        }

        public IServiceCollection AddUpdateControlPanel()
        {
            services.AddControlPanel<SharedClientModule, UpdateControlPanel, UpdateControlPanelState>()
                .WithAutoDiscovery<UpdateControlPanelDescriptor>()
                .WithSaveHandler<UpdateControlPanelSaveHandler>()
                .WithCancelHandler<UpdateControlPanelCancelHandler>()
                .WithResetHandler<UpdateControlPanelResetHandler>();

            return services;
        }
    }
}
