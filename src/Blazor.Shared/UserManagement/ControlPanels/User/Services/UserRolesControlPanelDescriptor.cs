using Blazor.Shared.UserManagement.ControlPanels.User.Components;
using Sdk.Client.ControlPanels.Services;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.UserManagement.ControlPanels.User.Services;

internal sealed class UserRolesControlPanelDescriptor : IControlPanelDescriptor<UserRolesControlPanel>
{
    public string Title => CommonVocabulary.RolePlural;
    public Uri? IconUrl => null;
    public int? Position => 1;
    public bool ShowInNavigation => false;
}
