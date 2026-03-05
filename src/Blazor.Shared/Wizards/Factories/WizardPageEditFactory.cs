using Blazor.Shared.Wizards.Models;
using Sdk.Client.Wizards.Services;

namespace Blazor.Shared.Wizards.Factories;

internal sealed class WizardPageEditFactory(IServiceProvider serviceProvider)
{
    public IWizardPageEdit CreateWizardPageEdit(IWizardPageState wizardPageState)
    {
        var wizardPageEditType = typeof(Models.WizardPageEdit<>).MakeGenericType(wizardPageState.GetType());

        if (Activator.CreateInstance(wizardPageEditType, [wizardPageState, serviceProvider]) is not IWizardPageEdit result)
            throw new InvalidCastException();

        return result;
    }
}
