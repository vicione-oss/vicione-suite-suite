using Blazor.Shared.Services;
using Blazor.Shared.UserInterface.ControlPanels.DateAndTime.Services;
using Sdk.Authorization;
using Sdk.Client.ControlPanels.Attributes;
using Sdk.Client.ControlPanels.Components;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;

namespace Blazor.Shared.UserInterface.ControlPanels.DateAndTime.Components;

[ControlPanelCategory<ControlPanelUserInterfaceCategoryDescriptor>]
[ControlPanelGroup<SecondaryControlPanelGroupDescriptor>]
[ModuleAuthorize(SharedClientModule.ModuleId, AccessLevel.Full)]
public sealed partial class DateAndTimeControlPanel : ControlPanelBase<DateAndTimeControlPanelState>
{
    private readonly string _timerIconCssClasses = MonochromeIconName.Timer.GetCssClasses().ToSpaceSeparated();
}
