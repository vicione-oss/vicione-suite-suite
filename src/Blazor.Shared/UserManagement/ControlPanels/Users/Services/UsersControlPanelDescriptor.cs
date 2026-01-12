using Blazor.Shared.Enums;
using Blazor.Shared.Extensions;
using Blazor.Shared.UserManagement.ControlPanels.Users.Components;
using Sdk.Client.ControlPanels.Services;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.UserManagement.ControlPanels.Users.Services;

internal sealed class UsersControlPanelDescriptor : IControlPanelDescriptor<UsersControlPanel>
{
    public string Title => CommonVocabulary.UserPlural;
    public string IconPath => SvgIcon.User.GetPath();
}
