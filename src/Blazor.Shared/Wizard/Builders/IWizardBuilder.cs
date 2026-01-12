using Blazor.Shared.Wizard.Components;
using Blazor.Shared.Wizard.Services;

namespace Blazor.Shared.Wizard.Builders;

public interface IWizardBuilder<TContext>
{
    IWizardPageBuilder<TContext, TComponent, TState> WithPage<TComponent, TState>()
        where TComponent : WizardPage<TState>
        where TState : class, IWizardPageState;

    IWizardPageBuilder<TContext, TComponent, WizardPageState> WithPage<TComponent>()
        where TComponent : WizardPage<WizardPageState>;
}
