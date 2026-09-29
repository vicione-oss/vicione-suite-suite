using Blazor.Shared.Enums;
using Blazor.Shared.Extensions;
using Blazor.Shared.UserManagement.ControlPanels.User.Components;
using Sdk.Client.ControlPanels.Services;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.UserManagement.ControlPanels.User.Services;

internal sealed class UserControlPanelDescriptor : IControlPanelDescriptor<UserControlPanel>
{
    public string Title => CommonVocabulary.User;
    public Uri? IconUrl => SvgIcon.User.GetPath();
    public bool ShowInNavigation => false;
}
