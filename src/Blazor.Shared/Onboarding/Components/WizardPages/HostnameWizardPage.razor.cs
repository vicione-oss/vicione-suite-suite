using Blazor.Shared.Onboarding.Services;
using Blazor.Shared.Wizard.Components;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;

namespace Blazor.Shared.Onboarding.Components.WizardPages;

public sealed partial class HostnameWizardPage : WizardPage<HostnameWizardPageState>
{
    private readonly string _descriptionBannerIconCssClass = MonochromeIconName.EditHost.GetCssClasses().ToSpaceSeparated();
}
