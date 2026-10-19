using Blazor.Shared.UserManagement.ControlPanels.User.Components;
using Sdk.Client.ControlPanels.Services;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.UserManagement.ControlPanels.User.Services;

internal sealed class UserPermissionsControlPanelDescriptor : IControlPanelDescriptor<UserPermissionsControlPanel>
{
    public string Title => TechnicalTerms.PermissionPlural;
    public Uri? IconUrl => null;
    public int? Position => 2;
    public bool ShowInNavigation => false;
}
