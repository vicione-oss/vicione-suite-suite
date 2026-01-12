using Sdk.Client.Wizards.Services;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Onboarding.Services;

internal sealed class HostnameWizardPageDescriptor : IWizardPageDescriptor
{
    public string Title => TechnicalTerms.Hostname;
}
