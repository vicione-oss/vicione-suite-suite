using Blazor.Shared.Profile.ControlPanels.Passkeys.Components;
using Blazor.Shared.Profile.ControlPanels.Passkeys.Services;
using Core.Shared.Passkeys;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.ControlPanels.Extensions;
using ViciOne.Ui.Blazor.Components.Grid.Extensions;

namespace Blazor.Shared.Profile.ControlPanels.Passkeys.Extensions;

internal static class IServiceCollectionExtensions
{
    public static IServiceCollection AddPasskeyControlPanels(this IServiceCollection services)
    {
        services.AddControlPanel<SharedClientModule, PasskeysControlPanel, PasskeysControlPanelState>()
            .WithAutoDiscovery<PasskeysControlPanelDescriptor>()
            .WithSaveHandler<PasskeysControlPanelSaveHandler>()
            .WithResetHandler<PasskeysControlPanelResetHandler>();

        services.AddControlPanel<SharedClientModule, AddPasskeyControlPanel, AddPasskeyControlPanelState>()
            .WithAutoDiscovery<AddPasskeyControlPanelDescriptor>()
            .WithSaveHandler<AddPasskeyControlPanelSaveHandler>()
            .WithResetHandler<AddPasskeyControlPanelResetHandler>();

        services.AddControlPanel<SharedClientModule, EditPasskeyControlPanel, EditPasskeyControlPanelState>()
            .WithAutoDiscovery<EditPasskeyControlPanelDescriptor>()
            .WithSaveHandler<EditPasskeyControlPanelSaveHandler>()
            .WithResetHandler<EditPasskeyControlPanelResetHandler>();

        services.AddGridItemSelectColumn()
            .AddGridItemSelection<string>(typeof(PasskeysControlPanelServiceKey));

        services.AddSingleton<IPasskeyHostSupport, PasskeyHostSupport>();

        return services;
    }
}
