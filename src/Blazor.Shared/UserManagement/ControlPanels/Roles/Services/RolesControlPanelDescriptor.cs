using Blazor.Shared.Enums;
using Blazor.Shared.Extensions;
using Blazor.Shared.UserManagement.ControlPanels.Roles.Components;
using Sdk.Client.ControlPanels.Services;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.UserManagement.ControlPanels.Roles.Services;

internal sealed class RolesControlPanelDescriptor : IControlPanelDescriptor<RolesControlPanel>
{
    public string Title => CommonVocabulary.RolePlural;
    public int? Position => 2;
    public Uri? IconUrl => SvgIcon.User.GetPath();
}
