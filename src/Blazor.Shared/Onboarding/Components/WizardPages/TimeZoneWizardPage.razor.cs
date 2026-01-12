using Blazor.Shared.Onboarding.Services;
using Blazor.Shared.Wizard.Components;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;

namespace Blazor.Shared.Onboarding.Components.WizardPages;

public sealed partial class TimeZoneWizardPage : WizardPage<TimeZoneWizardPageState>
{
    private readonly string _descriptionBannerIconCssClass = MonochromeIconName.Timer.GetCssClasses().ToSpaceSeparated();
}
