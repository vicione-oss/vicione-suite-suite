using Blazor.Shared.Module.ControlPanels.Services;
using Blazor.Shared.Module.Models;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.ControlPanels.Extensions;
using ViciOne.Ui.Blazor.Components.Grid.Extensions;

namespace Blazor.Shared.Module.ControlPanels.Extensions;

internal static class IServiceCollectionExtensions
{
    public static IServiceCollection AddModuleManagementControlPanel(this IServiceCollection services)
    {
        services.AddControlPanel<SharedClientModule, ModuleManagementControlPanel, ModuleManagementControlPanelState>()
            .WithAutoDiscovery<ModuleManagementControlPanelDescriptor>()
            .WithSaveHandler<ModuleManagementControlPanelSaveHandler>()
            .WithResetHandler<ModuleManagementControlPanelResetHandler>();

        services.AddGridItemSelectColumn()
                .AddGridItemSelection<ModuleMetadataModel>(typeof(InstalledModuleManagementControlPanelServiceKey))
                .AddGridItemSelection<ModuleMetadataModel>(typeof(AvailableModuleManagementControlPanelServiceKey))
                .AddGridItemSelection<string>(typeof(ModuleOptionDeclarationCollectionGridServiceKey), ServiceLifetime.Transient);

        return services;
    }

    public static IServiceCollection AddModuleDetailControlPanel(this IServiceCollection services)
    {
        services.AddControlPanel<SharedClientModule, ModuleDetailsControlPanel, ModuleDetailsControlPanelState>()
            .WithAutoDiscovery<ModuleDetailsControlPanelDescriptor>()
            .WithSaveHandler<ModuleDetailsControlPanelSaveHandler>()
            .WithResetHandler<ModuleDetailsControlPanelResetHandler>();

        return services;
    }
}
