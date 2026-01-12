using Blazor.Shared.Wizard.Factories;
using Blazor.Shared.Wizard.Models;
using Microsoft.Extensions.Logging;

namespace Blazor.Shared.Wizard.Services;

internal sealed partial class WizardStepFactory(ILogger<WizardStepFactory> logger) : IWizardStepFactory
{
    public IEnumerable<WizardStep> CreateAll(IEnumerable<IWizardPageRegistryItem> wizardPageRegistryItems)
    {
        var result = new List<WizardStep>();

        foreach (var (index, wizardPageRegistryItem) in wizardPageRegistryItems.Index())
        {
            try
            {
                result.Add(new WizardStep
                {
                    Number = index + 1,
                    Title = wizardPageRegistryItem.Descriptor.Title,
                    WizardPageRegistryItem = wizardPageRegistryItem
                });
            }
            catch (Exception ex)
            {
                CreateWizardStepFailed(logger, ex);
            }
        }

        return result;
    }

    [LoggerMessage(1, LogLevel.Error, "Create wizard step failed")]
    private static partial void CreateWizardStepFailed(ILogger<WizardStepFactory> logger, Exception exception);
}
