using Sdk.Client.ControlPanels.Services;
using ViciOne.Ui.Localization.Resources;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;

namespace Blazor.Shared.Profile.ControlPanels;

public sealed class ProfileCategoryDescriptor : IControlPanelCategoryDescriptor
{
    public string Title => CommonVocabulary.Profile;

    // TODO https://gitlab.com/vicione-oss/vicione/suite/suite/-/work_items/2675
    public string? IconCssClass
        => MonochromeIconName.UserInterfaceLight.GetCssClasses(MonochromeIconSize.SmallMedium)
            .ToSpaceSeparated();

    public int? Position => 2;
}
