using Blazor.Shared.Onboarding.Services;
using Sdk.Client.Wizards.Components;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;

namespace Blazor.Shared.Onboarding.Components.WizardPages;

public sealed partial class PasswordWizardPage : WizardPage<PasswordWizardPageState>
{
    private readonly string _descriptionBannerIconCssClass = MonochromeIconName.AddUser.GetCssClasses().ToSpaceSeparated();
}
