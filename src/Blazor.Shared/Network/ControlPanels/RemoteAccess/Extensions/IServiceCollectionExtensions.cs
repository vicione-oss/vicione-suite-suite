using Blazor.Shared.Network.ControlPanels.RemoteAccess.Components;
using Blazor.Shared.Network.ControlPanels.RemoteAccess.Services;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.ControlPanels.Extensions;

namespace Blazor.Shared.Network.ControlPanels.RemoteAccess.Extensions;

internal static class IServiceCollectionExtensions
{
    public static IServiceCollection AddRemoteAccessControlPanel(this IServiceCollection services)
    {
        services.AddControlPanel<SharedClientModule, RemoteAccessControlPanel, RemoteAccessControlPanelState>()
            .WithSaveHandler<RemoteAccessControlPanelSaveHandler>()
            .WithResetHandler<RemoteAccessControlPanelResetHandler>();

        return services;
    }
}
