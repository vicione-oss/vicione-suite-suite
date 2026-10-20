using Blazor.Shared.Connections.ControlPanels.Connections;
using Blazor.Shared.Connections.ControlPanels.Connections.Services;
using Blazor.Shared.Connections.ControlPanels.Tags;
using Blazor.Shared.Connections.ControlPanels.Tags.Services;
using Blazor.Shared.Connections.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sdk.Client.ControlPanels.Extensions;
using Sdk.Client.Services;
using ViciOne.Ui.Blazor.Components.CheckBox.Extensions;
using ViciOne.Ui.Blazor.Components.Grid.Extensions;

namespace Blazor.Shared.Connections.Extensions;

public static class IServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddConnectionManagement()
            => services
                .AddConnectionControlPanel()
                .AddConnectionsControlPanel()
                .AddConnectionsTagsControlPanel()
                .AddTagControlPanel();

        internal IServiceCollection AddConnectionServices()
        {
            services.TryAddScoped<ISuiteConnectionService, SuiteConnectionService>();

            // Suite has to provide <see cref="Sdk.Client.Services.IConnectionService"/> used by modules
            services.TryAddScoped<IConnectionService>(s => s.GetRequiredService<ISuiteConnectionService>());

            return services;
        }

        internal IServiceCollection AddConnectionControlPanel()
        {
            services.AddControlPanel<SharedClientModule, ConnectionControlPanel, ConnectionControlPanelState>()
                .WithAutoDiscovery<ConnectionControlPanelDescriptor>()
                .WithSaveHandler<ConnectionControlPanelSaveHandler>()
                .WithResetHandler<ConnectionControlPanelResetHandler>();

            services.AddConnectionServices();

            services.AddScoped<TestConnectionService>();

            return services;
        }

        internal IServiceCollection AddConnectionsControlPanel()
        {
            services.AddControlPanel<SharedClientModule, ConnectionsControlPanel, ConnectionsControlPanelState>()
                .WithAutoDiscovery<ConnectionsControlPanelDescriptor>()
                .WithSaveHandler<ConnectionsControlPanelSaveHandler>()
                .WithResetHandler<ConnectionsControlPanelResetHandler>();

            services.AddConnectionServices();

            services.AddGridItemSelectColumn()
                .AddGridItemSelection<Guid>(typeof(ConnectionsControlPanelServiceKey));

            return services;
        }

        internal IServiceCollection AddConnectionsTagsControlPanel()
        {
            services.AddControlPanel<SharedClientModule, ConnectionsTagsControlPanel, ConnectionsTagsControlPanelState>()
                .WithAutoDiscovery<ConnectionsTagsControlPanelDescriptor>()
                .WithSaveHandler<ConnectionsTagsControlPanelSaveHandler>()
                .WithResetHandler<ConnectionsTagsControlPanelResetHandler>();

            services.AddConnectionServices();

            services.AddGridItemSelectColumn()
                .AddGridItemSelection<Guid>(typeof(ConnectionsTagsControlPanelServiceKey));

            return services;
        }

        internal IServiceCollection AddTagControlPanel()
        {
            services.AddControlPanel<SharedClientModule, TagControlPanel, TagControlPanelState>()
                .WithAutoDiscovery<TagControlPanelDescriptor>()
                .WithSaveHandler<TagControlPanelSaveHandler>()
                .WithResetHandler<TagControlPanelResetHandler>();

            services.AddConnectionServices();

            services.AddCheckBox();

            return services;
        }
    }
}
