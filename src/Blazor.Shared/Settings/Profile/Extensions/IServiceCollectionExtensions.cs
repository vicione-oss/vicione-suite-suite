using Blazor.Shared.Profile.ControlPanels.ExternalIdProviders;
using Blazor.Shared.Profile.ControlPanels.Passkeys;
using Microsoft.Extensions.DependencyInjection;
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

        services.AddControlPanel<SharedClientModule, RenamePasskeyControlPanel, RenamePasskeyControlPanelState>()
            .WithAutoDiscovery<RenamePasskeyControlPanelDescriptor>()
            .WithSaveHandler<RenamePasskeyControlPanelSaveHandler>()
            .WithResetHandler<RenamePasskeyControlPanelResetHandler>();

        services.AddControlPanel<SharedClientModule, ExternalIdProvidersControlPanel, ExternalIdProvidersControlPanelState>()
            .WithAutoDiscovery<ExternalIdProvidersControlPanelDescriptor>();

        services.AddGridItemSelectColumn()
            .AddGridItemSelection<string>(typeof(PasskeyControlPanelServiceKey));

        return services;
    }
}
