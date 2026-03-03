using Blazor.Shared.UserManagement.ControlPanels.User.Components;
using Blazor.Shared.UserManagement.ControlPanels.User.Models;
using Blazor.Shared.UserManagement.ControlPanels.User.Services;
using Blazor.Shared.UserManagement.Extensions;
using Blazor.Shared.UserManagement.Models;
using Blazor.Shared.UserManagement.Services;
using Blazor.Shared.Validation.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.ControlPanels.Extensions;
using ViciOne.Ui.Blazor.Components.Grid.Extensions;

namespace Blazor.Shared.UserManagement.ControlPanels.User.Extensions;

internal static class IServiceCollectionExtensions
{
    public static IServiceCollection AddUserControlPanel(this IServiceCollection services)
    {
        services.AddTransient<IClaimsProvider, ClaimsProvider>();

        services.AddUsernameValidator()
            .AddPasswordValidator()
            .AddRepeatPasswordValidator()
            .AddEmailValidator()
            .AddPhoneNumberValidator();

        services.AddControlPanel<SharedClientModule, UserControlPanel, UserControlPanelState>()
            .WithAutoDiscovery<UserControlPanelDescriptor>()
            .WithSaveHandler<UserControlPanelSaveHandler>()
            .WithResetHandler<UserControlPanelResetHandler>();

        services.AddGridItemSelectColumn();
        services.AddGridItemSelection<string>(typeof(UserControlPanelServiceKey));
        services.AddGridItemSelection<PermissionGridItemId>(typeof(UserControlPanelServiceKey));

        return services;
    }
}
