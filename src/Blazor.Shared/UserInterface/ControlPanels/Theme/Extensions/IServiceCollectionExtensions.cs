using Blazor.Shared.Extensions;
using Blazor.Shared.UserInterface.ControlPanels.Theme.Components;
using Blazor.Shared.UserInterface.ControlPanels.Theme.Services;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.ControlPanels.Extensions;

namespace Blazor.Shared.UserInterface.ControlPanels.Theme.Extensions;

internal static class IServiceCollectionExtensions
{
    public static IServiceCollection AddThemeControlPanel(this IServiceCollection services)
    {
        services.AddControlPanel<SharedClientModule, ThemeControlPanel, ThemeControlPanelState>()
            .WithAutoDiscovery<ThemeControlPanelDescriptor>()
            .WithSaveHandler<ThemeControlPanelSaveHandler>()
            .WithResetHandler<ThemeControlPanelResetHandler>();

        services.AddLoginDesignService();

        return services;
    }
}
