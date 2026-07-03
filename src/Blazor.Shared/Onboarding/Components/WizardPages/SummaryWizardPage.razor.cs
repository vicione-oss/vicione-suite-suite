using Blazor.Shared.Onboarding.Services;
using Sdk.Client.Modules;
using Sdk.Client.Wizards.Components;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;

namespace Blazor.Shared.Onboarding.Components.WizardPages;

public sealed partial class SummaryWizardPage : WizardPage<SummaryWizardPageState>
{
    private readonly string _descriptionBannerIconCssClass = MonochromeIconName.Instructions.GetCssClasses().ToSpaceSeparated();

    private readonly Uri _deviceImageSrc = GetImageSrc("device.svg");
    private readonly Uri _timeZoneImageSrc = GetImageSrc("time-zone.png");
    private readonly Uri _localNetworkImageSrc = GetImageSrc("local-network.svg");
    private readonly Uri _internetConnectionImageSrc = GetImageSrc("internet-connection.svg");
    private readonly Uri _dnsImageSrc = GetImageSrc("dns-server.svg"); // todo: update with correct image
    private readonly Uri _deviceRestartImageSrc = GetImageSrc("device-restart.svg");

    private static Uri GetImageSrc(string filename)
        => ModuleAssetHelper.GetModuleImageUrl<SharedClientModule>($"onboarding-wizard/summary-page/{filename}");
}
