using Blazor.Shared.Enums;
using Blazor.Shared.Extensions;
using Blazor.Shared.UserManagement.ControlPanels.Role.Components;
using Sdk.Client.ControlPanels.Services;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.UserManagement.ControlPanels.Role.Services;

internal sealed class RoleControlPanelDescriptor : IControlPanelDescriptor<RoleControlPanel>
{
    public string Title => CommonVocabulary.Role;
    public Uri? IconUrl => SvgIcon.User.GetPath();
    public bool ShowInNavigation => false;
}
