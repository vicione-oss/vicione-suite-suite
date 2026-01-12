using Blazor.Shared.UserInterface.ControlPanels.Language.Components;
using Blazor.Shared.UserInterface.ControlPanels.Language.Services;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.ControlPanels.Extensions;

namespace Blazor.Shared.UserInterface.ControlPanels.Language.Extensions;

internal static class IServiceCollectionExtensions
{
    public static IServiceCollection AddLanguageControlPanel(this IServiceCollection services)
    {
        services.AddControlPanel<SharedClientModule, LanguageControlPanel, LanguageControlPanelState>()
            .WithAutoDiscovery<LanguageControlPanelDescriptor>()
            .WithSaveHandler<LanguageControlPanelSaveHandler>()
            .WithResetHandler<LanguageControlPanelResetHandler>();

        return services;
    }
}
