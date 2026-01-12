using Blazor.Shared.Wizard.Components;
using Blazor.Shared.Wizard.Extensions;
using Blazor.Shared.Wizard.Models;
using Blazor.Shared.Wizard.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Blazor.Shared.Wizard.Builders;

internal sealed class WizardPageBuilder<TContext, TComponent, TState>(IServiceCollection services)
    : IWizardPageBuilder<TContext, TComponent, TState>
        where TComponent : WizardPage<TState>
        where TState : IWizardPageState
{
    public IWizardPageBuilder<TContext, TComponent, TState> WithAutoDiscovery<TDescriptor>()
        where TDescriptor : class, IWizardPageDescriptor
    {
        var contextType = typeof(TContext);
        var componentType = typeof(TComponent);

        var wizardPageInfo = new WizardPageInfo
        {
            ComponentType = componentType,
            StateType = typeof(TState),
            DescriptorType = typeof(TDescriptor),
            KeyedServiceKey = typeof(WizardPageServiceKey<,>).MakeGenericType(contextType, componentType)
        };

        services.AddKeyedScoped(contextType, (svc, serviceKey) => wizardPageInfo);

        services.AddWizardPageDescriptor(wizardPageInfo)
            .AddWizardPageState(wizardPageInfo);

        return this;
    }

    public IWizardPageBuilder<TContext, TComponent, TState> WithSaveHandler<TSaveHandler>()
        where TSaveHandler : class, IWizardPageSaveHandler<TState>
    {
        services.TryAddScoped<IWizardPageSaveHandler<TState>, TSaveHandler>();

        return this;
    }

    public IWizardPageBuilder<TContext, TComponent, TState> WithResetHandler<TResetHandler>()
        where TResetHandler : class, IWizardPageResetHandler<TState>
    {
        services.TryAddScoped<IWizardPageResetHandler<TState>, TResetHandler>();

        return this;
    }
}
