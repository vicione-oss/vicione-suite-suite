using Blazor.Shared.Onboarding.Models;
using Microsoft.AspNetCore.Components;
using Sdk.Client.Modules;
using Sdk.Client.Wizards.Components;
using Sdk.Client.Wizards.Services;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;

namespace Blazor.Shared.Onboarding.Components.WizardPages;

public sealed partial class WelcomeWizardPage : WizardPage<WizardPageState>
{
    private readonly string _descriptionBannerIconCssClass = MonochromeIconName.Wizard.GetCssClasses().ToSpaceSeparated();
    private readonly string _deviceImageSrc = GetImageSrc("device.svg");

    private bool _isWizardWithPasswordPage;

    [Inject] private IWizardPageRegistry<IOnboardingWizardContext> WizardPageRegistry { get; set; } = default!;

    protected override void OnInitialized()
        => _isWizardWithPasswordPage = WizardPageRegistry.Any(i => i.ComponentType == typeof(PasswordWizardPage));

    private static string GetImageSrc(string filename)
        => ModuleAssetHelper.GetModuleImagePath<SharedClientModule>($"onboarding-wizard/welcome-page/{filename}");
}
