using Sdk.Client.Wizards.Services;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Onboarding.Services;

internal sealed class SummaryWizardPageDescriptor : IWizardPageDescriptor
{
    public string Title => CommonVocabulary.Summary;
}
