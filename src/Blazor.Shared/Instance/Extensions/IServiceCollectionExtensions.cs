using Blazor.Shared.Instance.ControlPanels.Instances;
using Blazor.Shared.Instance.ControlPanels.Instances.Services;
using Blazor.Shared.Instance.ControlPanels.Repositories;
using Blazor.Shared.Instance.ControlPanels.Repositories.Services;
using Blazor.Shared.Instance.ControlPanels.Update;
using Blazor.Shared.Instance.ControlPanels.Update.Services;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.ControlPanels.Extensions;
using ViciOne.Ui.Blazor.Components.Grid.Extensions;
using InstanceControlPanel = Blazor.Shared.Instance.ControlPanels.Instances.InstanceControlPanel;
using ArtifactRepositoryControlPanel = Blazor.Shared.Instance.ControlPanels.Repositories.ArtifactRepositoryControlPanel;
using UpdateControlPanelResetHandler = Blazor.Shared.Instance.ControlPanels.Update.Services.UpdateControlPanelResetHandler;
using UpdateControlPanelSaveHandler = Blazor.Shared.Instance.ControlPanels.Update.Services.UpdateControlPanelSaveHandler;

namespace Blazor.Shared.Instance.Extensions;

internal static class IServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddInstanceManagement()
        {
            services.AddInstancesControlPanel()
                .AddInstanceControlPanel()
                .AddUpdateControlPanel()
                .AddArtifactRepositoryControlPanels();

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

        public IServiceCollection AddArtifactRepositoryControlPanels()
        {
            services.AddScoped<IArtifactRepositoryClientService, ArtifactRepositoryClientService>();

            services.AddControlPanel<SharedClientModule, ArtifactRepositoryControlPanel, ArtifactRepositoryControlPanelState>()
                .WithAutoDiscovery<ArtifactRepositoryControlPanelDescriptor>()
                .WithSaveHandler<ArtifactRepositoryControlPanelSaveHandler>()
                .WithResetHandler<ArtifactRepositoryControlPanelResetHandler>();

            services.AddControlPanel<SharedClientModule, ArtifactRepositoriesControlPanel, ArtifactRepositoriesControlPanelState>()
                .WithAutoDiscovery<ArtifactRepositoriesControlPanelDescriptor>()
                .WithSaveHandler<ArtifactRepositoriesControlPanelSaveHandler>()
                .WithResetHandler<ArtifactRepositoriesControlPanelResetHandler>();

            services.AddGridItemSelection<Guid>(typeof(ArtifactRepositoriesControlPanelServiceKey));

            return services;
        }
    }
}
