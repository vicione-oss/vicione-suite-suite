using Blazor.Shared.UserManagement.ControlPanels.Users.Components;
using Blazor.Shared.UserManagement.ControlPanels.Users.Models;
using Blazor.Shared.UserManagement.ControlPanels.Users.Services;
using Core.Shared.UserManagement.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.ControlPanels.Extensions;
using ViciOne.Ui.Blazor.Components.Grid.Extensions;

namespace Blazor.Shared.UserManagement.ControlPanels.Users.Extensions;

internal static class IServiceCollectionExtensions
{
    public static IServiceCollection AddUsersControlPanel(this IServiceCollection services)
    {
        services.AddControlPanel<SharedClientModule, UsersControlPanel, UsersControlPanelState>()
            .WithAutoDiscovery<UsersControlPanelDescriptor>()
            .WithSaveHandler<UsersControlPanelSaveHandler>()
            .WithResetHandler<UsersControlPanelResetHandler>();

        services.AddGridItemSelectColumn();
        services.AddGridItemSelection<UserName>(typeof(UsersControlPanelServiceKey));

        return services;
    }
}
