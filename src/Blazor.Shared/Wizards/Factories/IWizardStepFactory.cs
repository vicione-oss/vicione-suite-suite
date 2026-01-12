using Blazor.Shared.Wizards.Models;
using Sdk.Client.Wizards.Services;

namespace Blazor.Shared.Wizards.Factories;

internal interface IWizardStepFactory
{
    IEnumerable<WizardStep> CreateAll(IEnumerable<IWizardPageRegistryItem> wizardPageRegistryItems);
}
