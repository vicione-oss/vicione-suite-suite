using Blazor.Shared.UserManagement.ControlPanels.Role.Components;
using Blazor.Shared.UserManagement.ControlPanels.Role.Services;
using Blazor.Shared.UserManagement.ControlPanels.Roles.Models;
using Blazor.Shared.UserManagement.Models;
using Blazor.Shared.UserManagement.Services;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.ControlPanels.Extensions;
using ViciOne.Ui.Blazor.Components.Grid.Extensions;

namespace Blazor.Shared.UserManagement.ControlPanels.Role.Extensions;

internal static class IServiceCollectionExtensions
{
    public static IServiceCollection AddRoleControlPanel(this IServiceCollection services)
    {
        services.AddTransient<IClaimsProvider, ClaimsProvider>();
        services.AddTransient<IGridItemService, GridItemService>();
        services.AddTransient<IAvailableClaimsService, AvailibleClaimsService>();

        services.AddControlPanel<SharedClientModule, RoleControlPanel, RoleControlPanelState>()
            .WithAutoDiscovery<RoleControlPanelDescriptor>()
            .WithSharedState(out var roleControlPanelState)
            .WithSharedHandlers(b => b
                    .WithSaveHandler<RoleControlPanelSaveHandler>()
                    .WithResetHandler<RoleControlPanelResetHandler>(),
                out var roleControlPanelHandlers);

        services.AddControlPanel<SharedClientModule, RolePermissionsControlPanel, RoleControlPanelState>()
            .WithAutoDiscovery<RolePermissionsControlPanelDescriptor>()
            .WithSharedState(roleControlPanelState)
            .WithSharedHandlers(roleControlPanelHandlers);

        services.AddGridItemSelectColumn();
        services.AddGridItemSelection<PermissionGridItemId>(typeof(RolesControlPanelServiceKey));

        return services;
    }
}
