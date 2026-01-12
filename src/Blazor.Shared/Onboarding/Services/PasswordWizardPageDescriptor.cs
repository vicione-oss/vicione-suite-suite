using Sdk.Client.Wizards.Services;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Onboarding.Services;

internal sealed class PasswordWizardPageDescriptor : IWizardPageDescriptor
{
    public string Title => CommonVocabulary.Password;
    public int? Position => 2;
}
