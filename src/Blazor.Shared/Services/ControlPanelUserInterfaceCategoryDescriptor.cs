using Sdk.Client.ControlPanels.Services;
using ViciOne.Ui.Localization.Resources;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;

namespace Blazor.Shared.Services;

internal sealed class ControlPanelUserInterfaceCategoryDescriptor : IControlPanelCategoryDescriptor
{
    public string Title => TechnicalTerms.UserInterface;
    public string IconCssClass => MonochromeIconName.UserInterfaceLight.GetCssClasses(MonochromeIconSize.SmallMedium).ToSpaceSeparated();
    public Uri? IconUrl => null;
    public int? Position => 1;
}
