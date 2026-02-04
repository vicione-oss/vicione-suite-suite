using Blazor.Shared.Connections.ControlPanels;
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
                .AddGridItemSelection<Guid>(typeof(ConnectionsControlPanelServiceKey))
                .AddGridItemSelection<Guid>(typeof(TagsControlPanelPageContentServiceKey));

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
