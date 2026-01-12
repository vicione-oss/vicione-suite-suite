using Sdk.Client.Wizards.Services;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Onboarding.Services;

internal sealed class WelcomeWizardPageDescriptor : IWizardPageDescriptor
{
    public string Title => CommonVocabulary.Welcome;
    public int? Position => 1;
}
