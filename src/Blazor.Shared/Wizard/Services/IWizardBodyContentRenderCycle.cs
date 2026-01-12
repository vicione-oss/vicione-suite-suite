namespace Blazor.Shared.Wizard.Services;

internal interface IWizardBodyContentRenderCycle
{
    void SetFinished(IWizardPageRegistryItem wizardPageRegistryItem);
}
