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
            .WithSaveHandler<ModuleManagementControlPanelSaveHandler>()
            .WithResetHandler<ModuleManagementControlPanelCancelHandler>();

        services.TryAddScoped<IModuleManagementService, ModuleManagementService>();

        services.AddGridItemSelectColumn();
        services.AddGridItemSelection<string>(typeof(ModuleOptionDeclarationCollectionGridServiceKey), ServiceLifetime.Transient);

        return services;
    }
}
