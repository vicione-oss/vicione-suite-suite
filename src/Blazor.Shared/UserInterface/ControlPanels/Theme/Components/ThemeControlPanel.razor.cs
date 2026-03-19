using Blazor.Shared.Services;
using Blazor.Shared.UserInterface.ControlPanels.Theme.Services;
using Core.Shared.Instance.Contracts;
using Sdk.Authorization;
using Sdk.Client.ControlPanels.Attributes;
using Sdk.Client.ControlPanels.Components;
using ViciOne.Ui.Localization.Resources;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;

namespace Blazor.Shared.UserInterface.ControlPanels.Theme.Components;

[ControlPanelCategory<ControlPanelUserInterfaceCategoryDescriptor>]
[ControlPanelGroup<SecondaryControlPanelGroupDescriptor>]
[ModuleAuthorize(SharedClientModule.ModuleId, AccessLevel.Full)]
public sealed partial class ThemeControlPanel : ControlPanelBase<ThemeControlPanelState>
{
    private readonly Dictionary<LoginDesign, string> _loginDesigns = new()
    {
        { LoginDesign.Default, CommonVocabulary.Default },
        { LoginDesign.MoneoConnect, ProductNames.MoneoConnect }
    };

    private readonly string _showIconCssClasses = MonochromeIconName.Show.GetCssClasses().ToSpaceSeparated();
}
