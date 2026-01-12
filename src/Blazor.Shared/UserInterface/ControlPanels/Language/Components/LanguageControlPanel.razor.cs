using Blazor.Shared.Services;
using Blazor.Shared.UserInterface.ControlPanels.Language.Services;
using Microsoft.AspNetCore.Components;
using Sdk.Authorization;
using Sdk.Client.ControlPanels.Attributes;
using Sdk.Client.ControlPanels.Components;
using Sdk.Client.NotificationArea.Services;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;

namespace Blazor.Shared.UserInterface.ControlPanels.Language.Components;

[ControlPanelCategory<ControlPanelUserInterfaceCategoryDescriptor>]
[ControlPanelGroup<SecondaryControlPanelGroupDescriptor>]
[ModuleAuthorize(SharedClientModule.ModuleId, AccessLevel.Full)]
public sealed partial class LanguageControlPanel : ControlPanelBase<LanguageControlPanelState>
{
    private readonly string _languageIconCssClasses = MonochromeIconName.Language.GetCssClasses().ToSpaceSeparated();
    private readonly string _refreshIconCssClasses = MonochromeIconName.Refresh.GetCssClasses(MonochromeIconSize.Small).ToSpaceSeparated();

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private IActiveNotificationElementPolicy ActiveNotificationElementPolicy { get; set; } = default!;

    private void RefreshButtonClick()
    {
        ActiveNotificationElementPolicy.NoneActive();

        State.ShowLanguageSavedBanner = false;

        Navigation.NavigateTo(Navigation.BaseUri, forceLoad: true);
    }
}
