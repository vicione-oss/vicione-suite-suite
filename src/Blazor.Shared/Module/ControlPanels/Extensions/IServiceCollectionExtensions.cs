using Blazor.Shared.Module.Models;
using Blazor.Shared.Module.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sdk.Client.ControlPanels.Extensions;
using ViciOne.Ui.Blazor.Components.Grid.Extensions;

namespace Blazor.Shared.Module.ControlPanels.Extensions;

internal static class IServiceCollectionExtensions
{
    public static IServiceCollection AddModuleManagementControlPanel(this IServiceCollection services)
    {
        services.AddControlPanel<SharedClientModule, ModuleManagementControlPanel, ModuleManagementControlPanelState>()
            .WithAutoDiscovery<ModuleManagementControlPanelDescriptor>()
            .WithResetHandler<ModuleManagementControlPanelResetHandler>();

        services.AddControlPanel<SharedClientModule, ModuleDetailsControlPanel, ModuleDetailsControlPanelState>()
            .WithAutoDiscovery<ModuleDetailsControlPanelDescriptor>()
            .WithSaveHandler<ModuleDetailsControlPanelSaveHandler>()
            .WithResetHandler<ModuleDetailsControlPanelResetHandler>();

        services.TryAddScoped<IModuleManagementService, ModuleManagementService>();

        services.AddGridItemSelectColumn()
                .AddGridItemSelection<ModuleMetadataModel>(typeof(ModuleManagementControlPanelServiceKey))
                .AddGridItemSelection<string>(typeof(ModuleOptionDeclarationCollectionGridServiceKey), ServiceLifetime.Transient);

        return services;
    }
}
