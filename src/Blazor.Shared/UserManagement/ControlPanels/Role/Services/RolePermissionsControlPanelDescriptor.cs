using Blazor.Shared.Enums;
using Blazor.Shared.Extensions;
using Blazor.Shared.UserManagement.ControlPanels.Role.Components;
using Sdk.Client.ControlPanels.Services;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.UserManagement.ControlPanels.Role.Services;

internal sealed class RolePermissionsControlPanelDescriptor : IControlPanelDescriptor<RolePermissionsControlPanel>
{
    public string Title => TechnicalTerms.PermissionPlural;
    public Uri? IconUrl => SvgIcon.User.GetPath();
    public int? Position => 1;
    public bool ShowInNavigation => false;
}
