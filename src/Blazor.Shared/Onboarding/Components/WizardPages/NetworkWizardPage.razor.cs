using Blazor.Shared.Onboarding.Services;
using Blazor.Shared.Wizard.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;

namespace Blazor.Shared.Onboarding.Components.WizardPages;

public sealed partial class NetworkWizardPage : WizardPage<NetworkWizardPageState>
{
    private readonly string _descriptionBannerIconCssClass = MonochromeIconName.NetworkTopology.GetCssClasses().ToSpaceSeparated();
    private readonly string _youtubeLink = "https://www.youtube.com/watch?v=6SbfeMn4Fjc";

    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    private async Task MoneoSetupWizardButtonClick()
        => await JSRuntime.InvokeVoidAsync("open", _youtubeLink, "_blank");
}
