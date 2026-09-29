using Blazor.Shared.UserManagement.ControlPanels.OpenIdProvider.Components;
using Blazor.Shared.UserManagement.ControlPanels.OpenIdProvider.Services;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.ControlPanels.Extensions;

namespace Blazor.Shared.UserManagement.ControlPanels.OpenIdProvider.Extensions;

internal static class IServiceCollectionExtensions
{
    public static IServiceCollection AddOpenIdProviderControlPanel(this IServiceCollection services)
    {
        services.AddControlPanel<SharedClientModule, OpenIdProviderControlPanel, OpenIdProviderControlPanelState>()
            .WithAutoDiscovery<OpenIdProviderControlPanelDescriptor>()
            .WithSaveHandler<OpenIdProviderControlPanelSaveHandler>()
            .WithResetHandler<OpenIdProviderControlPanelResetHandler>();

        return services;
    }
}
