using Blazor.Shared.Wizard.Models;
using Blazor.Shared.Wizard.Services;

namespace Blazor.Shared.Wizard.Factories;

internal interface IWizardStepFactory
{
    IEnumerable<WizardStep> CreateAll(IEnumerable<IWizardPageRegistryItem> wizardPageRegistryItems);
}
