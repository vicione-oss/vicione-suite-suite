using Blazor.Shared.Wizard.Services;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Onboarding.Services;

internal sealed class TimeZoneWizardPageDescriptor : IWizardPageDescriptor
{
    public string Title => CommonVocabulary.TimeZone;
}
