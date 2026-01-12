using Sdk.Client.Wizards.Services;

namespace Blazor.Shared.Wizards.Services;

internal sealed class WizardPageRegistryFactory : IWizardPageRegistryFactory
{
    public IWizardPageRegistry<TContext> CreateWizardPageRegistry<TContext>()
        => new WizardPageRegistry<TContext>();
}
