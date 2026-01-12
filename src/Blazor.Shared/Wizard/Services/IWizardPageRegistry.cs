using Blazor.Shared.Wizard.Components;
using Sdk.Client.Services;

namespace Blazor.Shared.Wizard.Services;

public interface IWizardPageRegistry : IRegistry<IWizardPageRegistryItem>
{
    IWizardPageRegistryItem Add<TComponent, TState>(IWizardPageDescriptor descriptor, TState state)
        where TComponent : WizardPage<TState>
        where TState : IWizardPageState;
}

public interface IWizardPageRegistry<TContext> : IWizardPageRegistry;
