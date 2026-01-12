using Sdk.Client.Wizards.Services;

namespace Blazor.Shared.Wizards.Services;

public interface IWizardBodyContentRenderCycle
{
    bool Complete { get; }

    event Action? Completed;

    void SetFinished(IWizardPageRegistryItem wizardPageRegistryItem);
}
