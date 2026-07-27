using Blazor.Shared.Profile.ControlPanels.ExternalIdProviders;
using Blazor.Shared.Profile.ControlPanels.Passkeys.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.ControlPanels.Extensions;
using ExternalIdProvidersControlPanel = Blazor.Shared.Profile.ControlPanels.ExternalIdProviders.ExternalIdProvidersControlPanel;

namespace Blazor.Shared.Settings.Profile.Extensions;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddProfileManagement(this IServiceCollection services)
    {
        services.AddPasskeyControlPanels();

        services.AddControlPanel<SharedClientModule, ExternalIdProvidersControlPanel, ExternalIdProvidersControlPanelState>()
            .WithAutoDiscovery<ExternalIdProvidersControlPanelDescriptor>();

        return services;
    }
}
