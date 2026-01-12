using Blazor.Shared.Wizard.Components;
using Blazor.Shared.Wizard.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Blazor.Shared.Wizard.Builders;

internal sealed class WizardBuilder<TContext>(IServiceCollection services) : IWizardBuilder<TContext>
{
    public IWizardPageBuilder<TContext, TComponent, WizardPageState> WithPage<TComponent>()
        where TComponent : WizardPage<WizardPageState>
            => WithPage<TComponent, WizardPageState>();

    public IWizardPageBuilder<TContext, TComponent, TState> WithPage<TComponent, TState>()
        where TComponent : WizardPage<TState>
        where TState : class, IWizardPageState
            => new WizardPageBuilder<TContext, TComponent, TState>(services);
}
