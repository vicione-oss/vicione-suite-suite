using Blazor.Shared.Network.ControlPanels.Ntp.Components;
using Blazor.Shared.Network.ControlPanels.Ntp.Services;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.ControlPanels.Extensions;

namespace Blazor.Shared.Network.ControlPanels.Ntp.Extensions;

internal static class IServiceCollectionExtensions
{
    public static IServiceCollection AddNtpControlPanel(this IServiceCollection services)
    {
        services.AddControlPanel<SharedClientModule, NtpControlPanel, NtpControlPanelState>()
            .WithSaveHandler<NtpControlPanelSaveHandler>()
            .WithResetHandler<NtpControlPanelResetHandler>();

        return services;
    }
}
