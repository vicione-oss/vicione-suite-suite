using Blazor.Shared.Wizard.Components;
using Blazor.Shared.Wizard.Services;

namespace Blazor.Shared.Wizard.Builders;

public interface IWizardPageBuilder<TContext, TComponent, TState>
    where TComponent : WizardPage<TState>
    where TState : IWizardPageState
{
    /// <summary>
    /// Configures auto-discovery which adds <typeparamref name="TComponent"/> to
    /// <see cref="IWizardPageRegistry"/> with the given <typeparamref name="TDescriptor"/>
    /// and <typeparamref name="TState"/>.
    /// </summary>
    /// <remarks>
    /// The method will register <see cref="IWizardPageDescriptor"/> and
    /// <typeparamref name="TState"/> as <see cref="WizardPageServiceKey{TComponent}">keyed service</see>.
    /// </remarks>
    IWizardPageBuilder<TContext, TComponent, TState> WithAutoDiscovery<TDescriptor>()
        where TDescriptor : class, IWizardPageDescriptor;

    /// <summary>
    /// Configures <typeparamref name="TSaveHandler"/> for handling save requests in context of <typeparamref name="TComponent"/>
    /// </summary>
    IWizardPageBuilder<TContext, TComponent, TState> WithSaveHandler<TSaveHandler>()
        where TSaveHandler : class, IWizardPageSaveHandler<TState>;

    /// <summary>
    /// Configures <typeparamref name="TResetHandler"/> for handling reset requests in context of <typeparamref name="TComponent"/>
    /// </summary>
    IWizardPageBuilder<TContext, TComponent, TState> WithResetHandler<TResetHandler>()
        where TResetHandler : class, IWizardPageResetHandler<TState>;
}
