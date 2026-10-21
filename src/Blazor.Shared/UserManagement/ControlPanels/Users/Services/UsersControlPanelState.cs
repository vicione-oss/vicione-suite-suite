using Core.Shared.UserManagement.Contracts;
using Sdk.Client.ControlPanels.Services;
using ViciOne.SourceGenerators.NotifyChanged;

namespace Blazor.Shared.UserManagement.ControlPanels.Users.Services;

public sealed partial class UsersControlPanelState : ControlPanelState
{
    [NotifyChanged]
    internal partial List<UserProfile> Users { get; set; } = [];

    [NotifyChanged]
    internal partial bool ResetSelectedUsers { get; set; }

    internal List<UserProfile> DeletingUsers { get; } = [];
}
