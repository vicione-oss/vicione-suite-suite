using Sdk.Client.ControlPanels.Services;
using ViciOne.SourceGenerators.NotifyChanged;

namespace Blazor.Shared.UserManagement.ControlPanels.Roles.Services;

public sealed partial class RolesControlPanelState : ControlPanelState
{
    [NotifyChanged]
    internal partial List<Sdk.UserManagement.Contracts.Role> Roles { get; set; } = [];

    [NotifyChanged]
    internal partial bool ResetSelectedRoles { get; set; }

    internal List<Sdk.UserManagement.Contracts.Role> DeletingRoles { get; } = [];
}
