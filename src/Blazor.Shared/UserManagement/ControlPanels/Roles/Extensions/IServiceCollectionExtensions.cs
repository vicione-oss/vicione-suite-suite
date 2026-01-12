using Blazor.Shared.UserManagement.ControlPanels.Roles.Components;
using Blazor.Shared.UserManagement.ControlPanels.Roles.Models;
using Blazor.Shared.UserManagement.ControlPanels.Roles.Services;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.ControlPanels.Extensions;
using ViciOne.Ui.Blazor.Components.Grid.Extensions;

namespace Blazor.Shared.UserManagement.ControlPanels.Roles.Extensions;

internal static class IServiceCollectionExtensions
{
    public static IServiceCollection AddRolesControlPanel(this IServiceCollection services)
    {
        services.AddControlPanel<SharedClientModule, RolesControlPanel, RolesControlPanelState>()
            .WithAutoDiscovery<RolesControlPanelDescriptor>()
            .WithSaveHandler<RolesControlPanelSaveHandler>()
            .WithResetHandler<RolesControlPanelResetHandler>();

        services.AddGridItemSelectColumn();
        services.AddGridItemSelection<string>(typeof(RolesControlPanelServiceKey));

        return services;
    }
}
