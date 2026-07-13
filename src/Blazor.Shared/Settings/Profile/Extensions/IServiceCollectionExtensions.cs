using Blazor.Shared.Profile.ControlPanels.ExternalIdProviders;
using Blazor.Shared.Profile.ControlPanels.Passkeys;
using Core.Shared.Passkeys;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sdk.Client.ControlPanels.Extensions;
using ViciOne.Ui.Blazor.Components.Grid.Extensions;
using ExternalIdProvidersControlPanel = Blazor.Shared.Profile.ControlPanels.ExternalIdProviders.ExternalIdProvidersControlPanel;

namespace Blazor.Shared.Settings.Profile.Extensions;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddProfileManagement(this IServiceCollection services)
    {
        services.AddControlPanel<SharedClientModule, PasskeysControlPanel, PasskeysControlPanelState>()
            .WithAutoDiscovery<PasskeyControlPanelDescriptor>()
            .WithSaveHandler<PasskeysControlPanelSaveHandler>()
            .WithResetHandler<PasskeysControlPanelResetHandler>();

        services.AddControlPanel<SharedClientModule, AddPasskeyControlPanel, AddPasskeysControlPanelState>()
            .WithAutoDiscovery<AddPasskeyControlPanelDescriptor>()
            .WithSaveHandler<AddPasskeyControlPanelSaveHandler>()
            .WithResetHandler<AddPasskeyControlPanelResetHandler>();

        services.AddControlPanel<SharedClientModule, EditPasskeyControlPanel, EditPasskeyControlPanelState>()
            .WithAutoDiscovery<EditPasskeyControlPanelDescriptor>()
            .WithSaveHandler<EditPasskeyControlPanelSaveHandler>()
            .WithResetHandler<EditPasskeyControlPanelResetHandler>();

        services.AddControlPanel<SharedClientModule, ExternalIdProvidersControlPanel, ExternalIdProvidersControlPanelState>()
            .WithAutoDiscovery<ExternalIdProvidersControlPanelDescriptor>();

        services.AddGridItemSelectColumn()
            .AddGridItemSelection<string>(typeof(PasskeyControlPanelServiceKey));

        services.AddSingleton<IPasskeyHostSupport, PasskeyHostSupport>();

        return services;
    }
}
