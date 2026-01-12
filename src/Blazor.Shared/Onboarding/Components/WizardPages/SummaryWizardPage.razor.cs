using Blazor.Shared.Onboarding.Services;
using Sdk.Client.Modules;
using Sdk.Client.Wizards.Components;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;

namespace Blazor.Shared.Onboarding.Components.WizardPages;

public sealed partial class SummaryWizardPage : WizardPage<SummaryWizardPageState>
{
    private readonly string _descriptionBannerIconCssClass = MonochromeIconName.Instructions.GetCssClasses().ToSpaceSeparated();

    private readonly string _deviceImageSrc = GetImageSrc("device.svg");
    private readonly string _timeZoneImageSrc = GetImageSrc("time-zone.png");
    private readonly string _localNetworkImageSrc = GetImageSrc("local-network.svg");
    private readonly string _internetConnectionImageSrc = GetImageSrc("internet-connection.svg");
    private readonly string _deviceRestartImageSrc = GetImageSrc("device-restart.svg");

    private static string GetImageSrc(string filename)
        => ModuleAssetHelper.GetModuleImagePath<SharedClientModule>($"onboarding-wizard/summary-page/{filename}");
}
