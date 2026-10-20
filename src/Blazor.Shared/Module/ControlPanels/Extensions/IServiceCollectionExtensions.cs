using Blazor.Shared.Dialogs.Extensions;
using Blazor.Shared.Module.ControlPanels.Services;
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
        // The following control panel has no content and is never edited, therefore no handlers are configured.
        // We register it nonetheless to have a parent for subsequent control panels registrations.
        services.AddControlPanel<SharedClientModule, ModuleManagementControlPanel, ModuleManagementControlPanelState>()
            .WithAutoDiscovery<ModuleManagementControlPanelDescriptor>();

        return services;
    }

    public static IServiceCollection AddModuleManagementInstalledControlPanel(this IServiceCollection services)
    {
        services.AddDialogs();

        services.TryAddScoped<IModuleManagementService, ModuleManagementService>();

        services.AddControlPanel<SharedClientModule, ModuleManagementInstalledControlPanel, ModuleManagementInstalledControlPanelState>()
            .WithAutoDiscovery<ModuleManagementInstalledControlPanelDescriptor>()
            .WithSaveHandler<ModuleManagementInstalledControlPanelSaveHandler>()
            .WithResetHandler<ModuleManagementInstalledControlPanelResetHandler>();

        services.AddGridItemSelectColumn()
            .AddGridItemSelection<ModuleMetadataModel>(typeof(InstalledModuleManagementControlPanelServiceKey));

        return services;
    }

    public static IServiceCollection AddModuleManagementBrowseControlPanel(this IServiceCollection services)
    {
        services.AddDialogs();

        services.TryAddScoped<IModuleManagementService, ModuleManagementService>();

        services.AddControlPanel<SharedClientModule, ModuleManagementBrowseControlPanel, ModuleManagementBrowseControlPanelState>()
            .WithAutoDiscovery<ModuleManagementBrowseControlPanelDescriptor>()
            .WithSaveHandler<ModuleManagementBrowseControlPanelSaveHandler>()
            .WithResetHandler<ModuleManagementBrowseControlPanelResetHandler>();

        services.AddGridItemSelectColumn()
            .AddGridItemSelection<ModuleMetadataModel>(typeof(AvailableModuleManagementControlPanelServiceKey));

        return services;
    }

    public static IServiceCollection AddModuleDetailControlPanel(this IServiceCollection services)
    {
        services.TryAddScoped<IModuleManagementService, ModuleManagementService>();

        services.AddControlPanel<SharedClientModule, ModuleDetailsControlPanel, ModuleDetailsControlPanelState>()
            .WithAutoDiscovery<ModuleDetailsControlPanelDescriptor>()
            .WithSaveHandler<ModuleDetailsControlPanelSaveHandler>()
            .WithResetHandler<ModuleDetailsControlPanelResetHandler>();

        services.AddGridItemSelectColumn()
            .AddGridItemSelection<string>(typeof(ModuleOptionDeclarationCollectionGridServiceKey), ServiceLifetime.Transient);

        return services;
    }
}
